# Remote state configuration.
# Bootstrap: create the storage account and container first, then uncomment and set key per environment.
# Example key: switchback.dev.tfstate or switchback.prod.tfstate
# See infra/README.md for bootstrap instructions.

# terraform {
#   backend "azurerm" {
#     resource_group_name  = "tfstate-rg"
#     storage_account_name = "tfstateswitchback"
#     container_name       = "tfstate"
#     key                  = "switchback.dev.tfstate"
#   }
# }
