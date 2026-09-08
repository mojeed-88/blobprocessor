resource "azurerm_resource_group" "main" {
  name     = "rg-fastship-${var.environment}"
  location = var.location
}

resource "azurerm_storage_account" "main" {
  name                = var.storage_account_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"

  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false

  public_network_access_enabled = true
}

resource "azurerm_storage_container" "invoices" {
  name                  = "invoices"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

data "azapi_resource" "table_service" {
  type      = "Microsoft.Storage/storageAccounts/tableServices@2022-09-01"
  parent_id = azurerm_storage_account.main.id
  name      = "default"
}

resource "azapi_resource" "processed_invoices" {
  type      = "Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01"
  parent_id = data.azapi_resource.table_service.id
  name      = "ProcessedInvoices"

  body = {
    properties = {
      signedIdentifiers = []
    }
  }
}

resource "azapi_resource" "invoice_dead_letters" {
  type      = "Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01"
  parent_id = data.azapi_resource.table_service.id
  name      = "InvoiceDeadLetters"

  body = {
    properties = {
      signedIdentifiers = []
    }
  }
}

resource "azurerm_application_insights" "main" {
  name                = "func-fastship-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  application_type    = "web"
  workspace_id        = var.log_analytics_workspace_id

  retention_in_days          = 90
  internet_ingestion_enabled = true
  internet_query_enabled     = true
  sampling_percentage        = 0
}

resource "azurerm_service_plan" "function" {
  name                = var.service_plan_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  os_type  = "Linux"
  sku_name = "FC1"
}


resource "azurerm_function_app_flex_consumption" "main" {
  name                = var.function_app_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.function.id
  tags = {
    "hidden-link: /app-insights-resource-id" = "${azurerm_resource_group.main.id}/providers/microsoft.insights/components/${azurerm_application_insights.main.name}"
  }

  storage_container_type     = "blobContainer"
  storage_container_endpoint = "${azurerm_storage_account.main.primary_blob_endpoint}app-package-funcfastshipdev-3525354"

  storage_authentication_type = "SystemAssignedIdentity"

  runtime_name    = "dotnet-isolated"
  runtime_version = "10.0"

  maximum_instance_count = 100
  instance_memory_in_mb  = 2048

  public_network_access_enabled = true
  https_only                    = false

  client_certificate_mode = "Required"

  app_settings = {
    "AzureWebJobsStorage__accountName" = azurerm_storage_account.main.name
    "AzureWebJobsStorage__credential"  = "managedidentity"
    "APP_ENVIRONMENT"                  = "Development"
    "BusinessTableEndpoint"            = trimsuffix(azurerm_storage_account.main.primary_table_endpoint, "/")
  }

  identity {
    type = "SystemAssigned"
  }

  site_config {
    minimum_tls_version               = "1.2"
    http2_enabled                     = true
    ip_restriction_default_action     = "Allow"
    scm_ip_restriction_default_action = "Allow"

    application_insights_connection_string = azurerm_application_insights.main.connection_string
  }
}


resource "azurerm_role_assignment" "function_storage_table_data_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_function_app_flex_consumption.main.identity[0].principal_id
}


resource "azurerm_role_assignment" "function_storage_queue_data_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_function_app_flex_consumption.main.identity[0].principal_id
}


resource "azurerm_role_assignment" "function_storage_blob_data_owner" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_function_app_flex_consumption.main.identity[0].principal_id
}


resource "azurerm_eventgrid_event_subscription" "invoices" {
  name  = "evsub-fastship-invoices-${var.environment}"
  scope = azurerm_storage_account.main.id

  event_delivery_schema = "EventGridSchema"

  included_event_types = [
    "Microsoft.Storage.BlobCreated"
  ]

  subject_filter {
    subject_begins_with = "/blobServices/default/containers/invoices/blobs/"
    subject_ends_with   = ""
    case_sensitive      = false
  }

  webhook_endpoint {
    url = "https://${azurerm_function_app_flex_consumption.main.name}.azurewebsites.net/runtime/webhooks/blobs?functionName=Host.Functions.BlobProcessor&code=${var.blob_extension_key}"

    max_events_per_batch              = 1
    preferred_batch_size_in_kilobytes = 64
  }

  retry_policy {
    max_delivery_attempts = 30
    event_time_to_live    = 1440
  }
}