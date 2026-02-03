# OAuth Setup for Trailhead Switchback

This document describes how to register OAuth applications for Gmail and Microsoft 365 so that users can connect their accounts. Store client secrets in Azure Key Vault; do not commit them to source control.

---

## Gmail (Google Cloud Console)

### 1. Create a project

1. Go to [Google Cloud Console](https://console.cloud.google.com/).
2. Create a new project or select an existing one.
3. Enable the **Gmail API**: APIs & Services → Library → search "Gmail API" → Enable.

### 2. Configure the OAuth consent screen

1. APIs & Services → OAuth consent screen.
2. Choose **External** (or Internal for workspace-only).
3. Fill in App name, User support email, Developer contact.
4. Scopes: Add the following:
   - `https://www.googleapis.com/auth/gmail.readonly` – read mail
   - `https://www.googleapis.com/auth/gmail.modify` – apply labels (no delete)
5. Save.

### 3. Create OAuth 2.0 credentials

1. APIs & Services → Credentials → Create Credentials → OAuth client ID.
2. Application type: **Web application**.
3. Name: e.g. "Switchback".
4. **Authorized redirect URIs** – add one per environment:
   - **Local (tunnel):** `https://YOUR-TUNNEL-URL/api/auth/gmail/callback`  
     Example: `https://abc123.ngrok.io/api/auth/gmail/callback`
   - **Production:** `https://YOUR-FUNCTION-APP.azurewebsites.net/api/auth/gmail/callback`  
     Example: `https://func-prod-switchback-xxx.azurewebsites.net/api/auth/gmail/callback`
5. Create. Copy the **Client ID** and **Client Secret**.

### 4. App settings (Function App)

| Setting | Description |
|--------|-------------|
| `Gmail:ClientId` | OAuth 2.0 Client ID |
| `Gmail:ClientSecret` | OAuth 2.0 Client secret (prefer Key Vault reference) |
| `Gmail:RedirectUri` | Must match exactly one authorized redirect URI (e.g. `https://func-xxx.azurewebsites.net/api/auth/gmail/callback`) |

For local development, set `Gmail:RedirectUri` to your tunnel URL (e.g. ngrok) and add that URL in the Google Console as an authorized redirect URI.

---

## Microsoft 365 (Entra ID / Azure AD)

### 1. Register an app

1. Go to [Azure Portal](https://portal.azure.com/) → Microsoft Entra ID (or Azure Active Directory) → App registrations → New registration.
2. Name: e.g. "Switchback".
3. Supported account types: as needed (e.g. "Accounts in any organizational directory and personal Microsoft accounts").
4. **Redirect URI** – Web, add one per environment:
   - **Local (tunnel):** `https://YOUR-TUNNEL-URL/api/auth/m365/callback`
   - **Production:** `https://YOUR-FUNCTION-APP.azurewebsites.net/api/auth/m365/callback`
5. Register. Note the **Application (client) ID** and **Directory (tenant) ID**.

### 2. Add API permissions

1. App registration → Your app → API permissions → Add a permission.
2. Microsoft Graph → Delegated permissions. Add:
   - **Mail.Read** – read user mail
   - **Mail.ReadWrite** – move messages to folders
   - **offline_access** – refresh tokens
3. Grant admin consent if required for your org.

### 3. Create a client secret

1. App registration → Your app → Certificates & secrets → New client secret.
2. Description: e.g. "Switchback Function App".
3. Expiry: as per policy. Copy the **Value** (secret) immediately; it is not shown again.

### 4. App settings (Function App)

| Setting | Description |
|--------|-------------|
| `M365:ClientId` | Application (client) ID |
| `M365:ClientSecret` | Client secret value (prefer Key Vault reference) |
| `M365:TenantId` | Directory (tenant) ID (use `common` for multi-tenant) |
| `M365:RedirectUri` | Must match exactly one redirect URI (e.g. `https://func-xxx.azurewebsites.net/api/auth/m365/callback`) |

For local development, set `M365:RedirectUri` to your tunnel URL and add that URL in the app registration redirect URIs.

---

## Local development (callbacks)

Google and Microsoft redirect the user’s browser to your **callback URL** after consent. Your Function App must be reachable at that URL.

- **Option A – Tunnel (ngrok, etc.):** Run a tunnel that forwards `https://xxx.ngrok.io` to `http://localhost:7071`. Use `https://xxx.ngrok.io/api/auth/gmail/callback` (and same for M365) as the redirect URI in both Google and Microsoft, and set `Gmail:RedirectUri` / `M365:RedirectUri` to the same. Start the Functions host with `func start` and use the tunnel URL when testing.
- **Option B – Deploy to dev:** Deploy the Function App to Azure (e.g. dev slot). Use the deployed host URL as the redirect URI in both consoles and in app settings.

---

## Key Vault references

In the Function App configuration (Azure Portal or Terraform), you can reference secrets from Key Vault instead of storing plaintext:

- `Gmail:ClientSecret` = `@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/GmailClientSecret/)`
- `M365:ClientSecret` = `@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/M365ClientSecret/)`

Ensure the Function App’s Managed Identity has **Get** permission on secrets in the Key Vault.

---

## Function App: Auth redirect (WebBaseUrl)

After OAuth callback, the Function App redirects the user back to the admin Web app. Set:

| Setting | Description |
|--------|-------------|
| `Auth:WebBaseUrl` | Base URL of the admin Web app (e.g. `https://your-web-app.azurewebsites.net` or `https://localhost:5001` for local). Used to build the redirect URL after Connect (e.g. `{WebBaseUrl}/Connections?connected=Gmail`). |

## Web app: Functions API and storage

The admin Web app needs:

| Setting | Description |
|--------|-------------|
| `ConnectionStrings:TableStorage` | Same storage connection string as the Function App (for User and ProviderConnection tables). |
| `Functions:BaseUrl` | Base URL of the Function App (e.g. `https://func-xxx.azurewebsites.net` or `http://localhost:7071`). Used to build Connect/Disconnect and connections-list URLs. |

---

## Summary of required app settings

| Setting | Gmail | M365 |
|--------|--------|------|
| Client ID | `Gmail:ClientId` | `M365:ClientId` |
| Client Secret | `Gmail:ClientSecret` | `M365:ClientSecret` |
| Tenant ID | — | `M365:TenantId` |
| Redirect URI | `Gmail:RedirectUri` | `M365:RedirectUri` |

Redirect URIs must match exactly (including scheme and path) what is registered in Google Cloud Console and Entra ID.
