# Local Setup – Run Switchback Cursor Locally

This guide walks through getting keys from Google and Microsoft 365, configuring the app, and running the Function App and Web app locally so you can test end-to-end.

---

## Minimal local run (no Gmail/M365)

You can run and test the app locally **without** setting up Gmail or M365. You’ll be able to register, log in, use Rules (add/edit/delete/reorder), and see Activity (empty). Connections will show “Not connected” and Connect Gmail/M365 will fail until you add OAuth keys.

**What you need**

- .NET 8 SDK  
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)  
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (Storage Emulator) so `UseDevelopmentStorage=true` works  

**Steps**

1. **Start Azurite** (so blob/queue/table endpoints are available).  
   - VS Code: install “Azurite” and run **Azurite: Start** from the command palette.  
   - Or: `npx azurite --silent --location .azurite --debug .azurite/debug.log`

2. **Start the Function App** (in one terminal). From the repo root, build the Functions project first so `func` doesn’t pick up the solution’s 3 projects; then start the host with `--no-build`:
   ```bash
   cd src/Switchback.Functions
   dotnet build Switchback.Functions.csproj
   func host start --no-build
   ```
   Leave it running. Default URL: `http://localhost:7071`.

3. **Start the Web app** (in another terminal):
   ```bash
   cd src/Switchback.Web
   dotnet run
   ```
   The app listens on **all interfaces** at port **5050** (5000 is often used by macOS Control Center/AirPlay). Open **http://localhost:5050** on this machine, or **http://\<your-PC-IP\>:5050** from another device (see below).

4. **In the browser:** Register a user → Log in → open **Rules** (add rules, edit, delete, reorder) and **Activity** (empty). **Connections** will show “Not connected”; ignore Connect Gmail/M365 until you add keys (sections 2–3 below).

Existing `local.settings.json` (only `AzureWebJobsStorage` and `FUNCTIONS_WORKER_RUNTIME`) and `appsettings.Development.json` (TableStorage + BaseUrl) are enough. Tables are created automatically when the Function App starts. **Use the same storage for both:** set `AzureWebJobsStorage` in Functions to the same value as the Web’s `ConnectionStrings:TableStorage` (e.g. both `UseDevelopmentStorage=true`) so rules and users live in the same place.

**If you see “Expected 1 .csproj but found 3”:** run from `src/Switchback.Functions` and use `dotnet build Switchback.Functions.csproj` then `func host start --no-build` (step 2 above).

When you’re ready to test Gmail/M365 and OAuth, follow the rest of this guide (prerequisites, keys, tunnel, etc.).

**Testing from an external browser (phone, another PC):** In Development the Web app binds to `0.0.0.0:5050`. On the machine running the app, find its IP (e.g. `ipconfig` / `ifconfig` or Settings → Network). From the other device, open **http://\<that-IP\>:5050**. Ensure any local firewall allows inbound TCP on port 5050.

---

## 1. Prerequisites

- **.NET 8 SDK** – [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Azure Functions Core Tools v4** – `npm i -g azure-functions-core-tools@4 --unsafe-perm true` or [install](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- **Azurite** (optional) – Azure Storage Emulator for local Table Storage. [VS Code extension](https://marketplace.visualstudio.com/items?itemName=Azurite.azurite) or `npm i -g azurite`
- **ngrok** (or similar) – So Google/M365 can redirect to your local Functions host. [Download](https://ngrok.com/download). **Full step-by-step:** [ngrok tunnel setup](ngrok-tunnel.md).

---

## 2. Gmail (Google) Keys

### 2.1 Create a Google Cloud project

1. Go to [Google Cloud Console](https://console.cloud.google.com/).
2. Create a new project (e.g. "Switchback Cursor") or select an existing one.
3. **Enable the Gmail API:** APIs & Services → Library → search "Gmail API" → Enable.

### 2.2 OAuth consent screen

1. APIs & Services → **OAuth consent screen**.
2. Choose **External** (or Internal if workspace-only).
3. App name: e.g. "Switchback Cursor". Set User support email and Developer contact.
4. **Scopes** → Add:
   - `https://www.googleapis.com/auth/gmail.readonly`
   - `https://www.googleapis.com/auth/gmail.modify`
5. Save.

### 2.3 OAuth client credentials

1. APIs & Services → **Credentials** → **Create credentials** → **OAuth client ID**.
2. Application type: **Web application**.
3. Name: e.g. "Switchback Cursor Local".
4. **Authorized redirect URIs** → Add (you’ll set this to your ngrok URL in step 5):
   - `https://YOUR-NGROK-URL/api/auth/gmail/callback`  
   Example: `https://abc123.ngrok-free.app/api/auth/gmail/callback`
5. Create. Copy the **Client ID** and **Client secret** (you’ll put these in `local.settings.json`).

You’ll come back to add the exact ngrok URL once you have it.

---

## 3. Microsoft 365 (Entra ID) Keys

### 3.1 Register an app

1. Go to [Azure Portal](https://portal.azure.com/) → **Microsoft Entra ID** (or Azure Active Directory) → **App registrations** → **New registration**.
2. Name: e.g. "Switchback Cursor Local".
3. Supported account types: e.g. "Accounts in any organizational directory and personal Microsoft accounts".
4. **Redirect URI** → Platform: **Web** → URI (you’ll set to ngrok in step 5):
   - `https://YOUR-NGROK-URL/api/auth/m365/callback`
5. Register. Note **Application (client) ID** and **Directory (tenant) ID**.

### 3.2 API permissions

1. Your app → **API permissions** → **Add a permission**.
2. **Microsoft Graph** → **Delegated**.
3. Add: **Mail.Read**, **Mail.ReadWrite**, **offline_access**.
4. Grant admin consent if your org requires it.

### 3.3 Client secret

1. Your app → **Certificates & secrets** → **New client secret**.
2. Description: e.g. "Switchback Local". Copy the **Value** immediately (it’s not shown again).

You’ll put Client ID, Tenant ID, and secret in `local.settings.json`.

---

## 4. Azure OpenAI (optional – for rule classification)

Without Azure OpenAI, the pipeline runs but the classifier always returns NONE (no rule applied). To test classification:

1. Create an **Azure OpenAI** resource in the Azure Portal.
2. Deploy a model (e.g. gpt-4o-mini) and note the **deployment name**.
3. In the resource: **Keys and Endpoint** → copy **Endpoint** and **Key**.
4. In `local.settings.json` set:
   - `AzureOpenAI:Endpoint` (e.g. `https://your-resource.openai.azure.com/`)
   - `AzureOpenAI:ApiKey`
   - `AzureOpenAI:Deployment` (deployment name)

---

## 5. Local config files

### 5.1 Functions – `local.settings.json`

1. In the repo: `src/Switchback.Functions/`.
2. Copy `local.settings.example.json` to `local.settings.json` (the latter is gitignored).
3. Set **storage** (pick one):
   - **Azurite:** `"AzureWebJobsStorage": "UseDevelopmentStorage=true"` (default). Start Azurite so the Functions host can reach it.
   - **Real Azure Storage:** `"AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"`.
4. Fill in **Gmail** (from section 2):
   - `Gmail:ClientId`
   - `Gmail:ClientSecret`
   - `Gmail:RedirectUri` → must match exactly what you’ll add in Google (e.g. `https://YOUR-NGROK-URL/api/auth/gmail/callback`).
5. Fill in **M365** (from section 3):
   - `M365:ClientId`
   - `M365:ClientSecret`
   - `M365:TenantId` (often `common`)
   - `M365:RedirectUri` → e.g. `https://YOUR-NGROK-URL/api/auth/m365/callback`.
6. **Auth:WebBaseUrl** → URL of your Web app in the browser, e.g. `https://localhost:5001` or `http://localhost:5050`.
7. **Azure OpenAI** (optional): `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:Deployment`.

**Key Vault:** Leave `KeyVault:VaultUri` unset for local. The app uses development encryption (no Key Vault) when that’s not configured.

### 5.2 Web app – `appsettings.Development.json`

Already present; ensure it points to your local Functions and storage:

- `ConnectionStrings:TableStorage`: same as `AzureWebJobsStorage` (e.g. `UseDevelopmentStorage=true` with Azurite, or your Azure Storage connection string).
- `Functions:BaseUrl`: `http://localhost:7071` (Functions default).

---

## 6. Redirect URIs (tunnel URL)

For a full walkthrough (install ngrok, start tunnel, set redirect URIs, update config), see **[ngrok tunnel setup](ngrok-tunnel.md)**.

Quick checklist:

1. Start ngrok so that **https://YOUR-NGROK-URL** forwards to **http://localhost:7071** (see [ngrok-tunnel.md](ngrok-tunnel.md)).
2. In **Google Cloud** → your OAuth client → add redirect URI:  
   `https://YOUR-NGROK-URL/api/auth/gmail/callback`
3. In **Azure Portal** → your app registration → Authentication → add redirect URI:  
   `https://YOUR-NGROK-URL/api/auth/m365/callback`
4. In `local.settings.json`, set:
   - `Gmail:RedirectUri` = `https://YOUR-NGROK-URL/api/auth/gmail/callback`
   - `M365:RedirectUri` = `https://YOUR-NGROK-URL/api/auth/m365/callback`

---

## 7. Run locally

### 7.1 Storage and tables

- **Azurite:** Start it (e.g. from VS Code or `azurite --silent --location .azurite --debug .azurite/debug.log`).  
  Tables (Users, ProviderConnections, Rules, Activity, etc.) are created automatically when the Function App starts.
- **Real Azure Storage:** No emulator; tables are created on first run.

### 7.2 Start the Function App

From repo root:

```bash
cd src/Switchback.Functions
func start
```

Leave it running. It listens on `http://localhost:7071`.

### 7.3 Start the Web app

In another terminal:

```bash
cd src/Switchback.Web
dotnet run
```

Open the URL shown (e.g. `https://localhost:5001` or `http://localhost:5050`). Use **that** as `Auth:WebBaseUrl` if you use a different port.

### 7.4 Start the tunnel

Start ngrok forwarding to `http://localhost:7071`. Use the **https** URL for the redirect URIs and in `local.settings.json` as above. See [ngrok-tunnel.md](ngrok-tunnel.md) for full steps.

---

## 8. Test flow

1. Open the Web app in the browser (e.g. `https://localhost:5001`).
2. **Register** a user, then **Log in**.
3. Go to **Connections** → **Connect Gmail** (or **Connect Microsoft 365**). You’ll be sent to Google/Microsoft; after consent you’re redirected back. Tables are filled by the Functions app.
4. Go to **Rules** → add a rule (name, prompt, destination).
5. (Optional) Use **Activity** to see processed items. To trigger processing without Gmail Pub/Sub, call the manual endpoint, e.g.  
   `POST http://localhost:7071/api/process?userId=YOUR_USER_ID&provider=Gmail&messageId=AN_EMAIL_MESSAGE_ID`  
   (get `userId` from your user record; get a real Gmail message ID from Gmail API or a test message).

---

## 9. Remaining items (production / optional)

- **Admin Web app in Azure** – Terraform now includes a Linux Web app (`web-<env>-switchback-<implementation>-<suffix>`). After `terraform apply`, deploy the Web project to that app (e.g. VS Publish, `az webapp deployment source config-zip`, or GitHub Actions). See `infra/README.md`.
- **Production secrets** – In Azure, use Key Vault references in Function App and Web App settings instead of plaintext (e.g. `@Microsoft.KeyVault(SecretUri=...)`). Store OAuth client secrets and storage connection string in Key Vault; grant the Function App (and Web App if needed) access. See `docs/oauth-setup.md`.
- **M365 push** – Only Gmail has push (Pub/Sub + watch + renewal). For M365, use the manual endpoint `POST /api/process?userId=...&provider=M365&messageId=...` (e.g. from a scheduler or external webhook). A future Graph mail subscription could mirror Gmail push.
- **CD** – Optional: add a GitHub Actions (or other) workflow to build and deploy the Function App and Web App on push to `main`. Use `az functionapp deployment` and `az webapp deployment` or publish artifacts.

Tables are created automatically at Functions startup. For Key Vault (production), set `KeyVault:VaultUri` and ensure the Function App identity has access; leave it unset for local to use development encryption.
