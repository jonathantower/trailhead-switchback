# Trailhead Switchback – Infrastructure

Azure infrastructure for Trailhead Switchback is defined in Terraform. This folder supports multiple environments (dev, prod) and remote state.

## Prerequisites

- **Azure CLI** – [Install](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli). Used for authentication and (optionally) bootstrap.
- **Terraform** – [Install](https://www.terraform.io/downloads) 1.0 or later.
- **Azure subscription** – You need Contributor (or equivalent) access to create resources.

## Authenticate

```bash
az login
az account set --subscription "<subscription-id-or-name>"
```

## Initialize

From the `infra` directory:

```bash
cd infra
terraform init
```

If you configure remote state (see below), run `terraform init` again after uncommenting the backend block.

## Plan and Apply

**Dev:**

```bash
terraform plan -var-file=dev.tfvars
terraform apply -var-file=dev.tfvars
```

**Prod:**

```bash
terraform plan -var-file=prod.tfvars
terraform apply -var-file=prod.tfvars
```

Always run `plan` before `apply` and review the changes.

## Destroy

To tear down an environment:

```bash
terraform destroy -var-file=dev.tfvars
```

State is isolated per environment when using remote state (separate state key per env).

## Remote State (Bootstrap)

To use Azure Storage as the Terraform backend:

1. **Create state storage (one-time, per subscription/tenant):**
   - Resource group: e.g. `tfstate-rg`
   - Storage account: globally unique name, e.g. `tfstateswitchback<unique>`
   - Container: `tfstate`

2. **Uncomment and edit `backend.tf`:**
   - Set `resource_group_name`, `storage_account_name`, `container_name`.
   - Use a different `key` per environment, e.g. `switchback.dev.tfstate` and `switchback.prod.tfstate`.

3. **Re-run init:**
   ```bash
   terraform init -reconfigure
   ```

4. **Bootstrap note:** The first time you use a new backend, you may need to run `terraform apply` once with the backend block commented out to create the state storage resources, then uncomment and migrate state. Alternatively, create the storage account and container manually or with a one-off script.

## Variables

| Variable        | Description |
|----------------|-------------|
| `environment`  | Environment name (e.g. `dev`, `prod`). Used in resource naming and tags. |
| `location`     | Azure region (default: `East US`). |
| `name_suffix`  | Short unique suffix for globally unique names (e.g. `dev`, `prod`, or tenant initials). Lowercase, 3–8 chars. Required because storage account and Key Vault names are globally unique. |

## Resources Created

- **Resource group** – All resources are created in one RG per environment.
- **Storage account** – Used by the Function App (host) and for Azure Table Storage (app data).
- **Log Analytics workspace** – For Application Insights logs.
- **Application Insights** – Logging and monitoring for the Function App.
- **Key Vault** – Holds the RSA key for token envelope encryption and (optionally) secrets. No plaintext secrets in Function App settings where avoidable.
- **Key Vault key** – RSA key used for encrypting/decrypting OAuth tokens at rest.
- **Linux Consumption plan** – Hosting plan for Azure Functions (SKU Y1).
- **Linux Function App** – .NET 8 isolated worker; system-assigned Managed Identity; access to Key Vault (Get secrets, Unwrap/Wrap key).

## Required Resource Providers

Ensure these are registered (usually on by default):

- `Microsoft.OperationalInsights`
- `Microsoft.Insights`
- `Microsoft.KeyVault`
- `Microsoft.Storage`
- `Microsoft.Web`

To check or register:

```bash
az provider show -n Microsoft.Web --query "registrationState" -o tsv
az provider register -n Microsoft.Web
```

## Troubleshooting

- **Storage account name already exists:** Change `name_suffix` in your `.tfvars` (e.g. add a random suffix) – storage account names are globally unique.
- **Key Vault name invalid:** Key Vault names must be 3–24 characters, alphanumeric and hyphens. The `name_suffix` is used to keep names short and unique.
- **Permission errors on apply:** Ensure your Azure identity has Contributor (or equivalent) on the subscription or resource group. For Key Vault, Terraform needs permission to create access policies and secrets.
