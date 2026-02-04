# GitHub Actions Workflows

This directory contains CI/CD workflows for the Trailhead Switchback project.

## Workflows

### CI (`ci.yml`)

Runs on every push/PR to `main` or `cursor-built`:
- Builds the solution
- Runs tests

### Deploy Infrastructure (`deploy-infra.yml`)

Deploys Azure infrastructure using Terraform.

**Triggers:**
- Push to `main` (when `infra/` changes)
- Manual dispatch (workflow_dispatch)

**Manual usage:**
1. Go to Actions → Deploy Infrastructure → Run workflow
2. Select environment (`dev` or `prod`)
3. Select action (`plan`, `apply`, or `destroy`)

**Required secrets:**
- `AZURE_CREDENTIALS` - Azure service principal credentials (JSON)
- `TERRAFORM_STORAGE_ACCOUNT` - Storage account name for Terraform state
- `TERRAFORM_CONTAINER` - Container name for Terraform state (e.g. `tfstate`)
- `TERRAFORM_RESOURCE_GROUP` - Resource group name for Terraform state storage

### Deploy Function App (`deploy-functions.yml`)

Builds and deploys the Azure Functions app.

**Triggers:**
- Push to `main` (when Functions or Core code changes)
- Manual dispatch

**Manual usage:**
1. Go to Actions → Deploy Function App → Run workflow
2. Select environment (`dev` or `prod`)

**Required secrets:**
- `AZURE_CREDENTIALS` - Azure service principal credentials (JSON)
- `FUNCTION_APP_NAME_DEV` (optional) - Function App name for dev environment. If not set, uses naming convention `func-dev-switchback-cursor-dev`
- `FUNCTION_APP_NAME_PROD` (optional) - Function App name for prod environment. If not set, uses naming convention `func-prod-switchback-cursor-dev`

### Deploy Web App (`deploy-web.yml`)

Builds and deploys the admin Web app.

**Triggers:**
- Push to `main` (when Web or Core code changes)
- Manual dispatch

**Manual usage:**
1. Go to Actions → Deploy Web App → Run workflow
2. Select environment (`dev` or `prod`)

**Required secrets:**
- `AZURE_CREDENTIALS` - Azure service principal credentials (JSON)
- `WEB_APP_NAME_DEV` (optional) - Web App name for dev environment. If not set, uses naming convention `web-dev-switchback-cursor-dev`
- `WEB_APP_NAME_PROD` (optional) - Web App name for prod environment. If not set, uses naming convention `web-prod-switchback-cursor-dev`

## Setting up GitHub Secrets

### 1. Create Azure Service Principal

```bash
az ad sp create-for-rbac --name "github-actions-switchback" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
  --sdk-auth
```

Copy the JSON output - this is your `AZURE_CREDENTIALS` secret.

### 2. Set GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions → New repository secret:

**Required:**
- **`AZURE_CREDENTIALS`**: The JSON output from step 1

**For Infrastructure deployment (if using remote state):**
- **`TERRAFORM_STORAGE_ACCOUNT`**: Your Terraform state storage account name (e.g. `tfstateswitchbackxxx`)
- **`TERRAFORM_CONTAINER`**: Container name (e.g. `tfstate`)
- **`TERRAFORM_RESOURCE_GROUP`**: Resource group for Terraform state (e.g. `tfstate-rg`)

**Optional (for app deployments):**
- **`FUNCTION_APP_NAME_DEV`**: Exact Function App name for dev (if different from `func-dev-switchback-cursor-dev`)
- **`FUNCTION_APP_NAME_PROD`**: Exact Function App name for prod
- **`WEB_APP_NAME_DEV`**: Exact Web App name for dev (if different from `web-dev-switchback-cursor-dev`)
- **`WEB_APP_NAME_PROD`**: Exact Web App name for prod

If you don't set the app name secrets, workflows use naming conventions based on your Terraform `dev.tfvars` / `prod.tfvars` (`name_suffix` and `implementation`).

### 3. Set up GitHub Environments (optional)

For environment-specific secrets (e.g. different Azure credentials per env):

1. Go to Settings → Environments
2. Create `dev` and `prod` environments
3. Add environment-specific secrets if needed (e.g. `AZURE_CREDENTIALS_DEV`, `AZURE_CREDENTIALS_PROD`)

Then update the workflows to use `${{ secrets.AZURE_CREDENTIALS_DEV }}` etc.

## Deployment Flow

1. **Infrastructure first:** Run `deploy-infra.yml` with action `apply` to create Azure resources. After `terraform apply`, note the Function App and Web App names from the outputs (or Azure Portal).
2. **Set app names (optional):** Add GitHub secrets `FUNCTION_APP_NAME_DEV`, `WEB_APP_NAME_DEV` (and `_PROD` variants) with the exact resource names from Terraform. If not set, workflows use naming conventions.
3. **Deploy apps:** After infrastructure exists, push code changes or manually run `deploy-functions.yml` and `deploy-web.yml`

## Notes

- **App settings:** OAuth client IDs/secrets, redirect URIs, and Azure OpenAI settings are **not** deployed by these workflows. Set them manually in Azure Portal (Function App → Configuration, Web App → Configuration) or add them to Terraform `app_settings` blocks.
- **Key Vault references:** For production, use Key Vault references in app settings (e.g. `@Microsoft.KeyVault(SecretUri=...)`) instead of plaintext secrets.
- **First-time deployment:** After `terraform apply`, you may need to set app settings manually before the apps will work (OAuth redirect URIs, etc.).
