# Water Temperature Log — Torniojoki, Jarhoinen

Blazor Web App (.NET 10) to add and view water-temperature readings from the
Torniojoki river at Jarhoinen village. Anyone can view the readings; a small
allow-list of signed-in editors can add, edit, and delete them.

**Live:** https://watertemperatures-jouni.azurewebsites.net

The UI is localized into Finnish (default) and English. User-facing text lives in
`Resources/AppText.resx` / `AppText.fi.resx`, looked up per request through
`Resources/AppText.cs` from `CultureInfo.CurrentUICulture`. The header's FI/EN
links post to `/culture`, which sets the culture cookie and does a full page
reload — the culture is fixed for the lifetime of a Blazor circuit, so switching
languages can't just morph the DOM.

## Stack

- **Frontend + backend**: Blazor Web App, Interactive Server render mode (all C#).
- **Storage**: repository abstraction (`Data/ITemperatureRepository.cs`).
  - No Cosmos account endpoint configured → `InMemoryTemperatureRepository` (seeded sample data, lost on restart). This is the default for local dev.
  - Cosmos account endpoint configured → `CosmosTemperatureRepository` (Azure Cosmos DB for NoSQL). Selected automatically in `Program.cs`; no UI changes. Auth is Entra ID (RBAC) via `DefaultAzureCredential` — no account key anywhere.
- **Auth**: Azure App Service Easy Auth (Google sign-in) + an editor allow-list. The Google client secret lives in Key Vault, referenced from App Service configuration.
- **Identity**: App Service has a system-assigned managed identity, granted the Cosmos DB **Built-in Data Contributor** data-plane role and the **Key Vault Secrets User** role.
- **Hosting**: Azure App Service (Linux, .NET 10).
- **Monitoring**: Application Insights (workspace-based), wired via `Microsoft.ApplicationInsights.AspNetCore`.
- **CI/CD**: GitHub Actions — push to `main` builds and deploys automatically.

## Access control

- **Everyone** can view the readings.
- **Editors** (add/edit/delete) must sign in with Google **and** be on the allow-list
  in `appsettings.json` (`Auth:Editors`). The UI hides edit controls for non-editors,
  and the add/edit/delete handlers re-check the editor role server-side.

To add an editor, append their email to `Auth:Editors` and deploy. Each editor must
also be a Google **test user** on the OAuth consent screen until it is published.

## Data model (`Models/TemperatureReading.cs`)

- **One reading per day**: `Id` is derived from `MeasuredOn` (`yyyy-MM-dd`) and doubles
  as the Cosmos item id and partition key, so a second insert for the same date is rejected.
- **`Celsius` is `decimal`** so values like 12.5 are stored and compared exactly.
- **Audit fields**: `CreatedBy`/`CreatedAt`, `UpdatedBy`/`UpdatedAt`.
- **Soft delete**: `IsDeleted` (+ `DeletedBy`/`DeletedAt`). Deleted items stay in the
  store (hidden from lists) so a delete can be undone; re-adding that day revives the item.

## Run locally

```bash
dotnet run
```

Then open the URL shown in the console. Pages: `/` (list + graph), `/add`, `/edit/{id}`.

- **Storage**: with no Cosmos account endpoint set, it uses the in-memory seed data.
- **Auth**: Easy Auth does not run on localhost, so `Auth:DevAutoLoginEmail` in
  `appsettings.Development.json` signs you in as an editor for testing. Comment it out
  to experience the app as an anonymous viewer.

### Point local runs at Cosmos (optional)

Cosmos auth is Entra ID (RBAC), not a key, so there's no secret to store. Set the
endpoint and sign in with an account that has been granted the Cosmos DB **Built-in
Data Contributor** role on the account (`az cosmosdb sql role assignment create`):

```bash
dotnet user-secrets set "Cosmos:AccountEndpoint" "https://watertemperatures-jouni.documents.azure.com:443/"
az login
```

`DefaultAzureCredential` picks up the `az login` session locally. On startup the app
creates the database/container if missing and seeds the historical readings the first
time the container is empty.

## Configuration

| Setting | Purpose |
| --- | --- |
| `Cosmos:AccountEndpoint` | Empty → in-memory. Set → use Cosmos, authenticating via `DefaultAzureCredential` (managed identity in Azure, `az login` locally). |
| `Cosmos:Database` / `Cosmos:Container` | Cosmos names (default `WaterTemperatures` / `Readings`). |
| `Auth:Editors` | Emails allowed to add/edit/delete. |
| `Auth:DevAutoLoginEmail` | Development only — auto sign-in as this editor locally. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure only. Absent locally → telemetry collection is skipped entirely (not just inert — the package throws at startup if you set this without a valid value). |

No secrets live in `appsettings.json` or user-secrets anymore: Cosmos uses managed
identity/RBAC, and the one remaining secret (the Google OAuth client secret) lives in
Key Vault, referenced from App Service configuration as
`@Microsoft.KeyVault(SecretUri=...)`.

## Deployment

Deployment is automatic: **push to `main`** and the GitHub Actions workflow
(`.github/workflows/deploy.yml`) checks out, sets up .NET 10, restores, builds,
publishes, and deploys to App Service. Each stage is its own step for easy diagnosis.
It can also be triggered manually from the repo's **Actions** tab (`workflow_dispatch`).

Azure authentication uses **OIDC federated credentials** (no stored password); the
workflow reads three non-secret identifiers from repo secrets: `AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

Manual fallback if you ever need it:

```bash
dotnet publish -c Release -o ./publish
(cd publish && zip -qr ../app.zip .)
az webapp deploy -g rg-watertemperatures -n watertemperatures-jouni --src-path app.zip --type zip
rm -rf ./publish app.zip
```

### Azure resources

- Resource group `rg-watertemperatures`.
- Cosmos DB (serverless, NoSQL) `watertemperatures-jouni` in West Europe. Local auth
  (account keys) is unused — data-plane access is Entra ID/RBAC only, granted to the
  App Service's managed identity via the **Cosmos DB Built-in Data Contributor** role.
- App Service `watertemperatures-jouni` on a Linux **F1 (free)** plan in Sweden Central,
  with WebSockets enabled (for Blazor Server), a system-assigned managed identity, and
  Easy Auth (Google) configured to allow unauthenticated access.
- Key Vault `kv-watertemperatures` (RBAC authorization mode) holds the Google OAuth
  client secret. The App Service's managed identity has the **Key Vault Secrets User**
  role, scoped to the vault.
- Application Insights `appi-watertemperatures` (workspace-based, backed by Log
  Analytics workspace `law-watertemperatures`), both in Sweden Central.

## Project layout

- `Models/TemperatureReading.cs` — domain model (id/date, °C, note, audit + soft-delete fields).
- `Data/ITemperatureRepository.cs` — storage contract; `InMemory…` and `Cosmos…` implementations; `SeedData.cs` shared historical rows.
- `Auth/` — `AuthOptions` (editor allow-list) and `EasyAuthMiddleware` (reads the Easy Auth identity / dev fallback into `HttpContext.User`).
- `Resources/AppText.cs` + `.resx` / `.fi.resx` — all user-facing text, in Finnish and English.
- `Components/Pages/` — `Home` (list + graph), `AddReading`, `EditReading`.
- `Components/Layout/` — `MainLayout` (header, footer, page chrome), `LoginDisplay` (sign-in state), `LanguageSelector` (FI/EN switch), `ReconnectModal` (Blazor Server reconnect UI).
- `wwwroot/app.css` — theme tokens (light/dark palette as CSS custom properties) that the rest of the app's styling reads from.
- `wwwroot/fonts/` — self-hosted Newsreader (display) and Public Sans (UI) faces, subset to Latin/Latin-ext.
- `wwwroot/img/` — the Peltoaukea background artwork (desktop and mobile artboards), masked onto the page in `app.css`.
