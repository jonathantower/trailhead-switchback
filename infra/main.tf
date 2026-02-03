# Trailhead Switchback - Azure infrastructure
# Core resources: Resource Group, Storage, Function App, etc.
# See variables.tf for input variables; use -var-file=dev.tfvars or prod.tfvars.

terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.environment}-switchback"
  location = var.location
  tags = {
    environment = var.environment
    project     = "switchback"
  }
}

# Placeholder: additional resources (Storage, Function App, Key Vault, etc.)
# will be added in EPIC B.
