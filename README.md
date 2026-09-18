# FastShip — Azure Cloud & DevOps Engineering Project

> A cloud-native, event-driven invoice processing system built with **Azure Functions, .NET, Azure Storage, Event Grid, Managed Identity, Terraform, Application Insights, Azure Monitor, and GitHub Actions CI/CD**.

---

## Project Overview

**FastShip** is an end-to-end Azure Cloud and DevOps engineering project designed to demonstrate how a real cloud application can be built, secured, monitored, provisioned, deployed, and recovered using modern Azure and DevOps practices.

The system automatically processes invoice files uploaded to Azure Blob Storage.

When an invoice is uploaded:

1. Azure Storage generates a blob event.
2. Azure Event Grid detects the event.
3. Event Grid invokes the Azure Function.
4. The Function processes the invoice.
5. Processing state is stored in Azure Table Storage.
6. Failed processing can be retried and eventually recorded for dead-letter recovery.
7. Application telemetry is sent to Application Insights.
8. Azure Monitor provides operational monitoring and alerting.

The infrastructure is managed using **Terraform**, while **GitHub Actions** provides CI/CD using **Azure OpenID Connect (OIDC)** authentication instead of long-lived deployment credentials.

---

## Architecture

```text
                         ┌─────────────────────┐
                         │      Developer      │
                         └──────────┬──────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   GitHub Repository │
                         └──────────┬──────────┘
                                    │
                             Pull Request
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   GitHub Actions CI │
                         │ Build + Validation  │
                         └──────────┬──────────┘
                                    │
                                    ▼
                              Merge to Main
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   GitHub Actions CD │
                         └──────────┬──────────┘
                                    │
                       Azure OIDC Authentication
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
                    ▼                               ▼
             ┌─────────────┐                 ┌──────────────┐
             │  Terraform  │                 │ Function App │
             │Infrastructure│                │  Deployment  │
             └──────┬──────┘                 └──────┬───────┘
                    │                               │
                    └───────────────┬───────────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │       Azure         │
                         └─────────────────────┘
```

### Application Event Flow

```text
Invoice Upload
      │
      ▼
┌─────────────────────┐
│ Azure Blob Storage  │
│ invoices container  │
└──────────┬──────────┘
           │
           │ BlobCreated Event
           ▼
┌─────────────────────┐
│  Azure Event Grid   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   BlobProcessor     │
│   Azure Function    │
└──────────┬──────────┘
           │
           ▼
     Invoice Processing
           │
      ┌────┴────┐
      │         │
      ▼         ▼
Processed     Failure /
Invoices      Retry
Table           │
                ▼
        Poison Processing
                │
                ▼
       InvoiceDeadLetters
              Table

                │
                ▼
      Application Insights
                │
                ▼
          Azure Monitor
```

---

## Technology Stack

| Area | Technology |
|---|---|
| Language | C# / .NET 10 |
| Compute | Azure Functions |
| Hosting | Flex Consumption |
| Operating System | Linux |
| Architecture | .NET Isolated Worker |
| Object Storage | Azure Blob Storage |
| Processing State | Azure Table Storage |
| Eventing | Azure Event Grid |
| Authentication | Managed Identity |
| Authorization | Azure RBAC |
| Observability | OpenTelemetry |
| Application Monitoring | Application Insights |
| Infrastructure Monitoring | Azure Monitor |
| Dashboard | Azure Workbook |
| Infrastructure as Code | Terraform |
| Terraform Providers | AzureRM + AzAPI |
| Remote State | Azure Blob Storage |
| Source Control | Git / GitHub |
| CI/CD | GitHub Actions |
| GitHub → Azure Authentication | OpenID Connect (OIDC) |

---

# How FastShip Works

The main business workflow starts when an invoice file is uploaded to the private `invoices` container.

```text
Invoice
   ↓
Blob Storage
   ↓
Event Grid
   ↓
BlobProcessor
   ↓
InvoiceProcessor
   ↓
ProcessedInvoices
```

Azure Event Grid provides the event-driven connection between Storage and the Function App.

This means the application does not need to continuously poll the storage account looking for new invoices.

---

# Application Structure

The application contains several important components.

### `BlobProcessor.cs`

The main invoice-processing Azure Function.

It receives blob events, invokes the invoice-processing service, and produces structured application telemetry.

### `HealthCheck.cs`

Provides an HTTP health endpoint:

```text
/api/HealthCheck
```

The endpoint is used to verify that the deployed Function App is responding successfully.

It is also used by the deployment pipeline as a post-deployment validation check.

### `PoisonBlobProcessor.cs`

Handles poison/retry failures.

When processing cannot complete successfully after the runtime retry process, the poison handler records the failure for later investigation and recovery.

### `Program.cs`

Configures:

- Dependency Injection
- Application services
- OpenTelemetry
- Application Insights integration

### Services and Models

The service layer contains the invoice-processing and persistence logic, including:

- Invoice processing
- Processing-state management
- Idempotency handling
- Failure handling
- Dead-letter persistence

---

# Managed Identity

One of the most important security improvements in FastShip was replacing storage credentials with **Azure Managed Identity**.

The Azure Function uses a:

> **System-assigned Managed Identity**

Instead of storing an Azure Storage account key inside the Function App.

The Function identity receives the required Azure RBAC roles:

```text
Storage Blob Data Owner
Storage Queue Data Contributor
Storage Table Data Contributor
```

The Functions runtime uses identity-based configuration:

```text
AzureWebJobsStorage__accountName = stfastshipdev001
AzureWebJobsStorage__credential  = managedidentity
```

This removes the need to maintain a storage account key in the application configuration.

---

## Important Authentication Lesson

During development, the Function App experienced Azure Storage authentication failures even though Managed Identity was configured correctly.

The cause was a stale legacy setting:

```text
AzureWebJobsStorage
```

containing an old connection-string configuration.

It existed alongside:

```text
AzureWebJobsStorage__accountName
AzureWebJobsStorage__credential
```

The old setting caused Azure Functions to attempt the wrong authentication method.

Removing it restored Managed Identity authentication.

### Lesson learned

When migrating Azure Functions from connection-string authentication to Managed Identity:

> Do not leave the legacy `AzureWebJobsStorage` connection string alongside the identity-based configuration.

---

# Runtime Storage vs Business Storage

FastShip separates **Azure Functions runtime storage** from **business-data access**.

The Functions runtime uses:

```text
AzureWebJobsStorage__accountName
AzureWebJobsStorage__credential
```

Business services use an explicit Azure Table endpoint:

```text
BusinessTableEndpoint=https://stfastshipdev001.table.core.windows.net
```

This separation makes the architecture easier to understand, configure, troubleshoot, and maintain.

---

# Idempotency

Event-driven systems may receive the same event more than once.

FastShip therefore includes processing-state management to prevent an invoice from being incorrectly processed multiple times.

Processing information is stored in:

```text
ProcessedInvoices
```

The application can detect processing state and protect the workflow from conflicting or duplicate work.

This makes invoice processing more reliable than simply assuming every event will arrive exactly once.

---

# Retry and Failure Handling

Transient failures are expected in distributed cloud applications.

FastShip therefore supports retry-aware processing.

Testing included simulated transient failures so retry behavior could be observed through application telemetry.

When processing ultimately cannot complete successfully, the poison-message workflow provides a separate recovery path.

---

# Dead-Letter Recovery

Failed work is handled by:

```text
PoisonBlobProcessor
```

The failure is persisted to:

```text
InvoiceDeadLetters
```

This provides a durable record that can be investigated later instead of silently losing failed invoice-processing operations.

The design therefore provides:

- Duplicate awareness
- Retry visibility
- Durable failure records
- Better troubleshooting
- Recovery information

---

# Observability

FastShip includes application observability using:

```text
OpenTelemetry
        │
        ▼
Application Insights
        │
        ▼
Azure Monitor
```

Application functions produce structured telemetry rather than relying only on plain text logs.

For example, invoice processing records a custom dimension:

```text
ProcessingStatus
```

This allows operational queries to distinguish processing states.

---

# Azure Monitor

Azure Monitor is used to detect processing problems.

FastShip includes a scheduled-query alert that checks Application Insights for failed invoice-processing events.

Example KQL:

```kusto
traces
| extend ProcessingStatus = tostring(customDimensions["ProcessingStatus"])
| where ProcessingStatus == "Failed"
```

The monitoring configuration includes:

- Azure Monitor alert rule
- Action Group
- Application Insights
- Log Analytics integration

---

# FastShip Operations Dashboard

An Azure Workbook provides an operational dashboard for the development environment.

The dashboard includes:

### Total Requests

Shows the total number of requests.

### Failed Requests

Shows unsuccessful requests.

### Exceptions

Tracks application exceptions.

### Request Activity

Displays request activity over time.

### Invoice Processing

Groups invoice-processing telemetry by:

```text
ProcessingStatus
```

### Dead-Letter Activity

Tracks telemetry associated with dead-letter processing.

The dashboard is managed through Terraform instead of relying only on manual Azure Portal configuration.

---

# Storage Hardening

The Azure Storage configuration was hardened for the project scope.

The implementation includes:

- Private containers
- Blob public access disabled
- HTTPS-only communication
- TLS 1.2 minimum where configured
- Managed Identity authentication
- Azure RBAC authorization
- No storage account keys committed to source control

---

# Environment Configuration

FastShip separates environment configuration from application code.

For the development environment:

```text
APP_ENVIRONMENT=Development
```

Local development configuration remains separate from Azure-hosted configuration.

This makes the application easier to extend later into environments such as:

```text
Development
Staging
Production
```

---

# Infrastructure as Code

FastShip infrastructure is managed with **Terraform**.

Terraform is responsible for creating and managing the Azure application infrastructure.

Major Terraform-managed resources include:

```text
Resource Group
│
├── Storage Account
│   ├── invoices container
│   ├── Function package container
│   ├── ProcessedInvoices table
│   └── InvoiceDeadLetters table
│
├── Flex Consumption Service Plan
│
├── Azure Function App
│   └── System-assigned Managed Identity
│
├── RBAC Role Assignments
│
├── Application Insights
│
├── Azure Monitor Action Group
│
├── Processing Failure Alert
│
├── Operations Workbook
│
└── Event Grid Subscription
```

The project uses:

```text
Terraform 1.14.3
AzureRM Provider
AzAPI Provider
```

---

# Terraform State

Terraform normally stores FastShip state remotely in Azure Blob Storage.

```text
Terraform
    │
    ▼
Azure Storage Account
    │
    ▼
Private tfstate container
    │
    ▼
fastship-dev.tfstate
```

The Terraform backend is deliberately stored separately from the application resource group.

This prevents application-resource deletion from automatically destroying the Terraform state used to manage that application.

A recovery script is also included:

```text
scripts/bootstrap-terraform-backend.sh
```

It can recreate the basic remote-state infrastructure during disaster recovery.

> Terraform state files may contain sensitive information and must never be committed to the repository.

---

# GitHub Actions Authentication

FastShip uses **Azure OpenID Connect (OIDC)** for GitHub Actions authentication.

```text
GitHub Actions
      │
      │ OIDC token
      ▼
Microsoft Entra ID
      │
      ▼
Azure RBAC
      │
      ▼
Azure Resources
```

This avoids storing a long-lived Azure client secret in GitHub.

The GitHub deployment identity uses narrowly scoped Azure permissions rather than unnecessary subscription-wide access.

---

# Continuous Integration

The CI workflow validates changes before they are merged.

The general CI flow is:

```text
Pull Request
      │
      ▼
Checkout Repository
      │
      ▼
Restore Dependencies
      │
      ▼
Build .NET Application
      │
      ▼
Terraform Setup
      │
      ▼
Terraform Validation
      │
      ▼
Terraform Plan
```

This provides an automated quality gate before infrastructure/application changes progress further.

---

# Continuous Deployment

Changes merged into `main` trigger the deployment workflow.

```text
Push / Merge to main
        │
        ▼
Checkout Repository
        │
        ▼
Restore Dependencies
        │
        ▼
Build Application
        │
        ▼
Publish Application
        │
        ▼
Azure OIDC Login
        │
        ▼
Verify Azure Access
        │
        ▼
Terraform Init
        │
        ▼
Terraform Plan
        │
        ▼
Terraform Apply
        │
        ▼
Deploy Azure Function
        │
        ▼
Health Check
```

The health check verifies the deployed application through:

```text
/api/HealthCheck
```

This means a successful package upload alone is not treated as proof that the application is healthy.

---

# Deployment Safety

The project includes several deployment safeguards:

- Git-based change tracking
- Pull-request validation
- Automated application builds
- Terraform validation
- Terraform plan before apply
- Managed Identity
- OIDC authentication
- Post-deployment health checking
- Application telemetry
- Azure monitoring
- Alerting
- Git checkpoints before destructive infrastructure operations

A full end-to-end CI/CD test was successfully demonstrated during the original project implementation.

---

# Disaster Recovery

After completing the main project, I started an additional disaster-recovery exercise.

The goal was to answer an important infrastructure question:

> **Can the FastShip environment be completely rebuilt from code without depending on undocumented manual Azure Portal configuration?**

Before deleting infrastructure, the environment was audited against Terraform state.

The audit identified several recovery gaps.

---

## Recovery Gap 1 — Function Package Container

The Function App used a package container that existed in Azure but was not originally represented explicitly in Terraform.

The container was added to Terraform so it could be recreated automatically.

---

## Recovery Gap 2 — Monitoring Infrastructure

The audit identified monitoring resources that needed to be fully represented in Terraform.

The following were added/confirmed:

```text
Action Group
Processing Failure Alert
Operations Workbook
```

This improved the reproducibility of the monitoring environment.

---

## Recovery Gap 3 — Terraform Backend

Because Terraform state is required to rebuild infrastructure, the backend itself also needs a recovery process.

A bootstrap script was created:

```bash
scripts/bootstrap-terraform-backend.sh
```

This can recreate the backend:

```text
Resource Group
      │
      ▼
Storage Account
      │
      ▼
Private tfstate Container
```

---

## Recovery Gap 4 — Event Grid Bootstrap Dependency

Event Grid calls the Azure Functions blob webhook using the Function host's:

```text
blobs_extension
```

system key.

A newly recreated Function App generates a **new key**.

That creates a dependency:

```text
Function App must exist
        │
        ▼
Function code must run
        │
        ▼
Function host initializes
        │
        ▼
New blobs_extension key exists
        │
        ▼
Event Grid webhook can be created
```

This meant a clean rebuild could not safely create everything in one Terraform phase.

---

# Two-Phase Disaster-Recovery Design

Terraform was updated to support a two-phase recovery process.

## Phase 1 — Core Infrastructure

Event Grid is temporarily disabled:

```hcl
create_eventgrid_subscription = false
```

Terraform can then create:

```text
Resource Group
Storage
Tables
Function Package Container
Service Plan
Function App
Managed Identity
RBAC
Application Insights
Monitoring
Workbook
```

without requiring the new Function system key.

---

## Phase 2 — Event Grid

After deploying the application:

```text
Deploy Function Code
        │
        ▼
Function Host Initializes
        │
        ▼
Retrieve New blobs_extension Key
        │
        ▼
Supply Key Securely to Terraform
        │
        ▼
Create Event Grid Subscription
```

This removes the clean-rebuild dependency problem.

---

# Disaster-Recovery Test

The FastShip development resource group was deliberately deleted during the recovery exercise.

Azure confirmed that the application resource group no longer existed.

Terraform then generated the Phase 1 recovery plan:

```text
Plan: 15 to add, 0 to change, 0 to destroy.
```

The plan was reviewed before applying it.

Terraform successfully recreated the core infrastructure:

```text
Apply complete! Resources: 15 added, 0 changed, 0 destroyed.
```

This demonstrated that the core FastShip infrastructure could be reconstructed from Terraform after deletion.

---

# Current Disaster-Recovery Status

The original FastShip Cloud/DevOps project was completed before the disaster-recovery exercise began.

Current DR progress:

```text
Infrastructure Audit                    
Terraform Recovery Gaps Identified      
Package Container Added to Terraform    
Monitoring Added to Terraform           
Workbook Added to Terraform             
Backend Recovery Script Created         
Two-Phase Event Grid Recovery Designed  
Application Resource Group Deleted      
Core Infrastructure Rebuilt             
.NET Application Build                  
Function Deployment                     
Function Host Health Verification       
Event Grid Restoration                  
Remote State Restoration                
GitHub OIDC RBAC Restoration            
Final End-to-End Recovery Test          
```

The current recovery checkpoint is **Function host health verification** before the new `blobs_extension` key is retrieved and Event Grid is restored.

---

# Key Engineering Lessons

This project provided several important practical lessons.

### 1. Identity configuration must match runtime behavior

Simply assigning a Managed Identity is not enough. Application configuration must actually cause the runtime to use that identity.

### 2. Remove legacy authentication configuration

Old connection strings can override or interfere with identity-based configuration.

### 3. Separate runtime and business storage concerns

Azure Functions runtime storage and application business storage should be treated as separate concerns.

### 4. Build observability into the application

Logs, structured telemetry, health checks, dashboards, and alerts should be part of the architecture rather than added only after failures occur.

### 5. Design for duplicate events

Event-driven systems should not assume exactly-once delivery.

Idempotency is therefore an important application concern.

### 6. Failed work must remain recoverable

Retries help with temporary failures, while durable dead-letter records help investigate failures that cannot be resolved automatically.

### 7. Infrastructure as Code must represent the real environment

A Terraform configuration is not truly reproducible if important live resources exist only because somebody created them manually.

### 8. Protect Terraform state

Remote Terraform state is part of the infrastructure-management system and needs its own security and recovery strategy.

### 9. Prefer OIDC over long-lived deployment secrets

GitHub Actions can authenticate to Azure using federated identity rather than storing a reusable Azure client secret.

### 10. Disaster recovery exposes hidden dependencies

The `blobs_extension` Event Grid dependency was much easier to discover through a real rebuild exercise than through architecture diagrams alone.

---

# Security Principles

The repository should never contain:

```text
Storage account keys
Function host/system key values
Azure client secrets
Credential-bearing connection strings
Terraform state files
Sensitive environment files
```

The project instead uses:

```text
Managed Identity
Azure RBAC
GitHub OIDC
Protected GitHub secrets where required
Private storage containers
```

---

# Repository Structure

A simplified repository layout:

```text
blobprocessor/
│
├── BlobProcessor.cs
├── HealthCheck.cs
├── PoisonBlobProcessor.cs
├── Program.cs
├── host.json
├── blobprocessor.csproj
│
├── Models/
│
├── Services/
│
├── infra/
│   └── terraform/
│       ├── main.tf
│       ├── providers.tf
│       ├── variables.tf
│       └── terraform.tfvars
│
├── scripts/
│   └── bootstrap-terraform-backend.sh
│
└── .github/
    └── workflows/
        ├── ci.yml
        └── cd.yml
```

---

# Project Status

| Area | Status |
|---|---|
| Azure Functions application | ✅ Complete |
| Event-driven invoice processing | ✅ Complete |
| Managed Identity | ✅ Complete |
| Runtime/business storage separation | ✅ Complete |
| Observability | ✅ Complete |
| Monitoring and alerts | ✅ Complete |
| Operations dashboard | ✅ Complete |
| Idempotency | ✅ Complete |
| Retry handling | ✅ Complete |
| Dead-letter recovery | ✅ Complete |
| Storage hardening | ✅ Complete |
| Environment configuration | ✅ Complete |
| Terraform Infrastructure as Code | ✅ Complete |
| Terraform remote state | ✅ Complete |
| GitHub OIDC | ✅ Complete |
| CI workflow | ✅ Complete |
| CD workflow | ✅ Complete |
| End-to-end CI/CD validation | ✅ Complete |

---

# What This Project Demonstrates

FastShip demonstrates hands-on experience with:

```text
Azure Functions
.NET / C#
Azure Blob Storage
Azure Table Storage
Azure Event Grid
Managed Identity
Azure RBAC
Application Insights
Azure Monitor
Log Analytics
OpenTelemetry
Azure Workbooks
Terraform
Terraform Remote State
Infrastructure as Code
Git
GitHub
GitHub Actions
Azure OIDC
CI/CD
Idempotency
Retry Handling
Dead-Letter Recovery
Monitoring & Alerting
Troubleshooting
Disaster Recovery
```

---

# Project Journey

FastShip evolved through the following engineering stages:

```text
Application Development
        ↓
Azure Storage Integration
        ↓
Event-Driven Processing
        ↓
Managed Identity
        ↓
Runtime / Business Storage Separation
        ↓
Observability
        ↓
Monitoring & Alerts
        ↓
Idempotency & Recovery
        ↓
Storage Hardening
        ↓
Environment Configuration
        ↓
Terraform Infrastructure as Code
        ↓
Remote Terraform State
        ↓
GitHub Repository
        ↓
Azure OIDC
        ↓
Continuous Integration
        ↓
Continuous Deployment
        ↓
Deployment Validation
        ↓
End-to-End Testing
        ↓
Disaster-Recovery Engineering
```

---

## Conclusion

FastShip started as an Azure Functions invoice-processing application and evolved into a complete **Cloud and DevOps engineering project**.

The project demonstrates how application development connects with:

- Cloud infrastructure
- Identity and access management
- Event-driven architecture
- Infrastructure as Code
- Observability
- Monitoring
- Reliability engineering
- CI/CD
- Deployment security
- Troubleshooting
- Disaster recovery

The most important outcome of the project is not simply that the application runs in Azure.

It is that the system can be **built, secured, observed, deployed, troubleshot, and increasingly reproduced from code using established Cloud/DevOps practices**.

---

### FastShip

**Cloud-native invoice processing system built with Azure Functions, .NET, Terraform, Managed Identity, Event Grid, monitoring, and GitHub Actions CI/CD.**

---

## Author

Mojeed Tijani

Azure Cloud Infrastructure Engineer

## Certifications

• AZ-104 – Microsoft Azure Administrator

• KCNA – Kubernetes and Cloud Native Associate

• FinOps Certified Engineer


