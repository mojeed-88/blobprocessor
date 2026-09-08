variable "environment" {
  description = "Deployment environment name."
  type        = string
}

variable "location" {
  description = "Azure region where resources will be deployed."
  type        = string
}

variable "storage_account_name" {
  description = "Name of the Azure Storage account."
  type        = string
}

variable "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics workspace used by Application Insights."
  type        = string
}

variable "service_plan_name" {
  description = "Name of the Flex Consumption App Service plan."
  type        = string
}

variable "function_app_name" {
  description = "Name of the Azure Function App."
  type        = string
}

variable "blob_extension_key" {
  description = "System key used by Event Grid to call the Blob Trigger webhook."
  type        = string
  sensitive   = true
}