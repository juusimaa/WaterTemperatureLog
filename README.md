# Water Temperature Log — Torniojoki, Jarhoinen

Blazor Web App (.NET 10) to add and view water-temperature readings from the
Torniojoki river at Jarhoinen village. Anyone can view the readings; a small
allow-list of signed-in editors can add, edit, and delete them.

**Live:** https://watertemperatures-jouni.azurewebsites.net

User-facing text and location names live in `Resources/AppText.cs`. Finnish/English
localization is planned — see that file for the migration path to `.resx` resources.

## Stack

- **Frontend + backend**: Blazor Web App, Interactive Server render mode (all C#).
- **Storage**: repository abstraction (`Data/ITemperatureRepository.cs`).
  - No Cosmos connection string configured → `InMemoryTemperatureRepository` (seeded sample data, lost on restart). This is the default for local dev.
  - Cosmos connection string configured → `CosmosTemperatureRepository` (Azure Cosmos DB for NoSQL). Selected automatically in `Program.cs`; no UI changes.
- **Auth**: Azure App Service Easy Auth (Google sign-in) + an editor allow-list.
- **Hosting**: Azure App Service (Linux, .NET 10).
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

- **Storage**: with no Cosmos connection string, it uses the in-memory seed data.
- **Auth**: Easy Auth does not run on localhost, so `Auth:DevAutoLoginEmail` in
  `appsettings.Development.json` signs you in as an editor for testing. Comment it out
  to experience the app as an anonymous viewer.

### Point local runs at Cosmos (optional)

Store the connection string with user-secrets (never commit it):

```bash
dotnet user-secrets set "Cosmos:ConnectionString" "<connection-string>"
```

On startup the app creates the database/container if missing and seeds the historical
readings the first time the container is empty.

## Configuration

| Setting | Purpose |
| --- | --- |
| `Cosmos:ConnectionString` | Empty → in-memory. Set → use Cosmos. In Azure, set as app setting `Cosmos__ConnectionString`. |
| `Cosmos:Database` / `Cosmos:Container` | Cosmos names (default `WaterTemperatures` / `Readings`). |
| `Auth:Editors` | Emails allowed to add/edit/delete. |
| `Auth:DevAutoLoginEmail` | Development only — auto sign-in as this editor locally. |

Secrets never live in `appsettings.json`: locally use user-secrets, in Azure use App
Service configuration.

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
- Cosmos DB (serverless, NoSQL) `watertemperatures-jouni` in West Europe.
- App Service `watertemperatures-jouni` on a Linux **F1 (free)** plan in Sweden Central,
  with WebSockets enabled (for Blazor Server) and Easy Auth (Google) configured to
  allow unauthenticated access.

## Project layout

- `Models/TemperatureReading.cs` — domain model (id/date, °C, note, audit + soft-delete fields).
- `Data/ITemperatureRepository.cs` — storage contract; `InMemory…` and `Cosmos…` implementations; `SeedData.cs` shared historical rows.
- `Auth/` — `AuthOptions` (editor allow-list) and `EasyAuthMiddleware` (reads the Easy Auth identity / dev fallback into `HttpContext.User`).
- `Components/Pages/` — `Home` (list + graph), `AddReading`, `EditReading`.
- `Components/Layout/LoginDisplay.razor` — sign-in state and login/logout links.
