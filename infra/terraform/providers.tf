terraform {
  required_version = ">= 1.8.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }

    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-fastship-tfstate"
    storage_account_name = "stfastshiptfstate001"
    container_name       = "tfstate"
    key                  = "fastship-dev.tfstate"
    use_azuread_auth     = true
  }
}

provider "azurerm" {
  features {}

  storage_use_azuread = true
}