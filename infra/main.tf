# Trailhead Switchback - Azure infrastructure
# Core resources: Resource Group, Storage, Function App, Key Vault, App Insights.
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

locals {
  common_tags = {
    environment    = var.environment
    project        = "switchback"
    implementation = var.implementation
  }
  # Storage: 3-24 chars, lowercase alphanumeric. Use short impl suffix to fit (cursor->cur, copilot->cop, claude->cla).
  impl_short     = substr(var.implementation, 0, 3)
  storage_account_name = substr("st${var.environment}switchback${local.impl_short}${var.name_suffix}", 0, 24)
  function_app_name    = "func-${var.environment}-switchback-${var.implementation}-${var.name_suffix}"
  key_vault_name       = substr("kv-${var.environment}-switchback-${var.implementation}", 0, 24)
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.environment}-switchback-${var.implementation}"
  location = var.location
  tags     = local.common_tags
}

# Storage account for Azure Functions (host) and Table Storage (app data)
resource "azurerm_storage_account" "main" {
  name                     = substr(local.storage_account_name, 0, 24)
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = var.environment == "prod" ? "GRS" : "LRS"
  min_tls_version          = "TLS1_2"
  tags                     = local.common_tags
}

# Log Analytics Workspace (minimal, for App Insights)
resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${var.environment}-switchback-${var.implementation}-${var.name_suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = "PerGB2018"
  retention_in_days   = var.environment == "prod" ? 90 : 30
  tags                = local.common_tags
}

# Application Insights for logging
resource "azurerm_application_insights" "main" {
  name                = "appi-${var.environment}-switchback-${var.implementation}-${var.name_suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location             = azurerm_resource_group.rg.location
  workspace_id         = azurerm_log_analytics_workspace.main.id
  application_type     = "other"
  tags                 = local.common_tags
}

# Key Vault for secrets and encryption key (token envelope encryption)
resource "azurerm_key_vault" "main" {
  name                        = local.key_vault_name
  resource_group_name         = azurerm_resource_group.rg.name
  location                    = azurerm_resource_group.rg.location
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  sku_name                    = "standard"
  soft_delete_retention_days  = 7
  purge_protection_enabled   = var.environment == "prod"
  tags                        = local.common_tags
}

# Store storage account connection string in Key Vault so Function App can reference it (no plaintext secrets in app settings)
data "azurerm_client_config" "current" {}

# Terraform identity needs to set secrets and keys (e.g. storage connection string, encryption key) during apply
resource "azurerm_key_vault_access_policy" "terraform" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id
  secret_permissions = ["Get", "List", "Set", "Delete", "Recover", "Purge"]
  key_permissions    = ["Get", "List", "Create", "Decrypt", "Encrypt", "UnwrapKey", "WrapKey", "GetRotationPolicy"]
}

# RSA key in Key Vault for envelope encryption of OAuth tokens
resource "azurerm_key_vault_key" "token_encryption" {
  name         = "token-encryption-key"
  key_vault_id = azurerm_key_vault.main.id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["decrypt", "encrypt", "sign", "verify", "wrapKey", "unwrapKey"]
  depends_on   = [azurerm_key_vault_access_policy.terraform]
}

resource "azurerm_key_vault_secret" "storage_connection_string" {
  name         = "storage-connection-string"
  value        = azurerm_storage_account.main.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_key_vault_access_policy.terraform]
}

# Linux Consumption plan for Azure Functions (.NET 8 isolated worker)
resource "azurerm_service_plan" "functions" {
  name                = "plan-${var.environment}-switchback-${var.implementation}-${var.name_suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = "Y1"
  tags                = local.common_tags
}

# Linux Function App with system-assigned Managed Identity
resource "azurerm_linux_function_app" "main" {
  name                       = local.function_app_name
  resource_group_name        = azurerm_resource_group.rg.name
  location                   = azurerm_resource_group.rg.location
  service_plan_id            = azurerm_service_plan.functions.id
  storage_account_name       = azurerm_storage_account.main.name
  storage_account_access_key = azurerm_storage_account.main.primary_access_key
  tags                       = local.common_tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime  = true
    }
    use_32_bit_worker        = false
    ftps_state               = "Disabled"
    minimum_tls_version      = "1.2"
    vnet_route_all_enabled    = false
    application_insights_key               = azurerm_application_insights.main.instrumentation_key
    application_insights_connection_string = azurerm_application_insights.main.connection_string
  }

  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME"       = "dotnet-isolated"
    "WEBSITE_RUN_FROM_PACKAGE"       = "1"
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.main.connection_string
    # AzureWebJobsStorage: use Key Vault reference in production; for initial deploy use connection string
    "AzureWebJobsStorage" = azurerm_storage_account.main.primary_connection_string
    # AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, AzureOpenAI:Deployment should be set manually in Function App Configuration
    # or via Key Vault references if you have an existing Azure OpenAI resource
  }
}

# Function App Managed Identity: Get + Unwrap Key for token encryption, Get secrets
resource "azurerm_key_vault_access_policy" "function_app" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_function_app.main.identity[0].principal_id
  secret_permissions = ["Get", "List"]
  key_permissions    = ["Get", "UnwrapKey", "WrapKey"]
}

# App Service Plan for admin Web app (Linux, F1 free tier for dev; B1 for prod if you need always-on)
resource "azurerm_service_plan" "web" {
  name                = "plan-web-${var.environment}-switchback-${var.implementation}-${var.name_suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = var.environment == "prod" ? "B1" : "F1"
  tags                = local.common_tags
}

# Linux Web app (admin UI – Rules, Connections, Activity)
resource "azurerm_linux_web_app" "main" {
  name                = "web-${var.environment}-switchback-${var.implementation}-${var.name_suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  service_plan_id     = azurerm_service_plan.web.id
  https_only          = true
  tags                = local.common_tags

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
    ftps_state          = "Disabled"
    minimum_tls_version = "1.2"
    # F1/Free plan does not support always_on
    always_on = var.environment == "prod" ? true : false
  }

  app_settings = {
    "ConnectionStrings__TableStorage"       = azurerm_storage_account.main.primary_connection_string
    "Functions__BaseUrl"                    = "https://${azurerm_linux_function_app.main.default_hostname}"
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.main.connection_string
  }
}
