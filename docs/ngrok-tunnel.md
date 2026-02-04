# ngrok tunnel for local OAuth

Google and Microsoft OAuth redirect the user’s browser to a **public URL** after consent. Your Function App runs on `http://localhost:7071`, so you need a tunnel that exposes it as an HTTPS URL. ngrok does that.

---

## 1. Install ngrok

**macOS (Homebrew):**
```bash
brew install ngrok
```

**Or download:** [ngrok.com/download](https://ngrok.com/download) — unzip and put `ngrok` on your PATH.

**Sign up (required for HTTPS):** [dashboard.ngrok.com/signup](https://dashboard.ngrok.com/signup). Free tier is enough. After signup, copy your authtoken from the dashboard and run:
```bash
ngrok config add-authtoken YOUR_AUTH_TOKEN
```

---

## 2. Start the Function App first

The tunnel forwards traffic to `localhost:7071`, so the Functions host must be running **before** you start ngrok.

In one terminal:
```bash
cd src/Switchback.Functions
dotnet build Switchback.Functions.csproj
func host start --no-build
```

Leave it running. You should see: `Functions: http://localhost:7071`.

---

## 3. Start the ngrok tunnel

In a **second** terminal:
```bash
ngrok http 7071
```

You’ll see something like:
```
Forwarding   https://abc123def456.ngrok-free.app -> http://localhost:7071
```

**Copy the `https://` URL** (e.g. `https://abc123def456.ngrok-free.app`).  
- Free ngrok gives a **new URL each time** you run `ngrok http 7071`. If you restart ngrok, you must update the redirect URIs and `local.settings.json` with the new URL.
- Paid plans can reserve a fixed subdomain so you don’t have to re-configure.

---

## 4. Set redirect URIs in Google and Microsoft

Use your ngrok **base URL** (no path). Replace `YOUR-NGROK-URL` below with your actual URL (e.g. `abc123def456.ngrok-free.app` — no `https://` in the console fields that ask for “URI”).

### Google Cloud Console

1. [Google Cloud Console](https://console.cloud.google.com/) → your project → **APIs & Services** → **Credentials**.
2. Open your **OAuth 2.0 Client ID** (Web application).
3. Under **Authorized redirect URIs**, add:
   - `https://YOUR-NGROK-URL/api/auth/gmail/callback`  
   Example: `https://abc123def456.ngrok-free.app/api/auth/gmail/callback`
4. Save.

### Microsoft Entra ID (Azure AD)

1. [Azure Portal](https://portal.azure.com/) → **Microsoft Entra ID** → **App registrations** → your app.
2. **Authentication** → **Add a platform** → **Web** (or edit existing Web redirect URI).
3. **Redirect URI**, add:
   - `https://YOUR-NGROK-URL/api/auth/m365/callback`  
   Example: `https://abc123def456.ngrok-free.app/api/auth/m365/callback`
4. Save.

---

## 5. Update local.settings.json

In `src/Switchback.Functions/local.settings.json`, set the **exact** redirect URIs (same as above):

```json
"Gmail:RedirectUri": "https://YOUR-NGROK-URL/api/auth/gmail/callback",
"M365:RedirectUri": "https://YOUR-NGROK-URL/api/auth/m365/callback",
```

Replace `YOUR-NGROK-URL` with your ngrok host (e.g. `abc123def456.ngrok-free.app`).

**Auth:WebBaseUrl** is where the user lands after Connect (the Web app). Use your Web app URL, e.g.:

- `http://localhost:5050` (if the Web app runs on port 5050), or  
- `https://localhost:5001` if you use HTTPS locally.

```json
"Auth:WebBaseUrl": "http://localhost:5050"
```

No need to change **Functions:BaseUrl** in the Web app’s `appsettings.Development.json` — keep it as `http://localhost:7071`. The Web app talks to the Functions host on localhost; only the OAuth **redirect** from Google/Microsoft goes through ngrok to your machine, then ngrok forwards to `localhost:7071`.

---

## 6. Restart the Function App (if it was already running)

After editing `local.settings.json`, restart the Functions host so it picks up the new redirect URIs:

1. In the terminal where `func host start` is running, press **Ctrl+C**.
2. Start again: `func host start --no-build`.

You do **not** need to restart ngrok when you change `local.settings.json` — only when you change the **port** (e.g. from 7071 to something else).

---

## 7. Test the flow

1. Start **Azurite** (if you use `UseDevelopmentStorage=true`).
2. Start the **Function App** (`func host start --no-build`).
3. Start **ngrok** (`ngrok http 7071`) and note the HTTPS URL.
4. Start the **Web app** (`dotnet run` in `src/Switchback.Web`).
5. In the browser: open the Web app (e.g. `http://localhost:5050`) → Log in → **Connections** → **Connect Gmail** (or **Connect Microsoft 365**).
6. You should be sent to Google/Microsoft; after consent, you’re redirected to the ngrok URL (e.g. `https://xxx.ngrok-free.app/api/auth/gmail/callback`), which tunnels to your Function App, then the app redirects you back to the Web app (e.g. `http://localhost:5050/Connections`).

---

## Troubleshooting

| Issue | What to check |
|-------|----------------|
| **redirect_uri_mismatch** (Google) | The URI in the error must **exactly** match what’s in Google Console and in `Gmail:RedirectUri` (including `https://`, host, and path). |
| **AADSTS50011: Redirect URI mismatch** (Microsoft) | Same as above: `M365:RedirectUri` and the redirect URI in Entra ID must match exactly. |
| **502 Bad Gateway** or connection errors from ngrok | Function App must be running on port 7071 before you start ngrok. Restart `func host start` and try again. |
| **New ngrok URL after restart** | Free ngrok assigns a new URL each run. Update Google redirect URI, Microsoft redirect URI, and `local.settings.json` with the new host, then restart the Function App. |

---

## Summary

| Step | Command / action |
|------|-------------------|
| 1 | Install ngrok, sign up, `ngrok config add-authtoken YOUR_TOKEN` |
| 2 | Start Function App: `func host start --no-build` (port 7071) |
| 3 | Start tunnel: `ngrok http 7071` → copy the `https://` URL |
| 4 | Add `https://YOUR-NGROK-URL/api/auth/gmail/callback` in Google Console |
| 5 | Add `https://YOUR-NGROK-URL/api/auth/m365/callback` in Entra ID |
| 6 | Set `Gmail:RedirectUri` and `M365:RedirectUri` in `local.settings.json` |
| 7 | Set `Auth:WebBaseUrl` to your Web app URL (e.g. `http://localhost:5050`) |
| 8 | Restart Function App, then test Connect from the Web app |
