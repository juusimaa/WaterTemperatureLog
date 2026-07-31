# AZ-204 coverage

This project doubles as hands-on practice for **AZ-204: Developing Solutions for
Microsoft Azure**. This document tracks which exam skill areas the app actually
exercises — with pointers to the code and Azure resources that prove it — and what's
deliberately left out. It's a snapshot, not the exam outline itself; skill areas and
weightings below reflect the outline at the time this was written and may drift.

## Develop Azure compute solutions

- **Create and configure an Azure App Service Web App.** Linux App Service
  (`watertemperatures-jouni`), F1 plan, WebSockets enabled for Blazor Server's
  SignalR circuit. See the [Azure resources](README.md#azure-resources) section.
- **Configure app settings.** `Cosmos:AccountEndpoint`, `Auth:Editors`, and
  `APPLICATIONINSIGHTS_CONNECTION_STRING` all flow from App Service configuration in
  production and `appsettings*.json`/user-secrets locally — see the
  [Configuration](README.md#configuration) table.
- **Implement autoscale / deployment slots / containers, Azure Functions.** Not
  covered yet. No slots (single F1 instance), no containers, no Functions app.

## Develop for Azure storage

- **Develop solutions that use Azure Cosmos DB for NoSQL.** `Data/CosmosTemperatureRepository.cs`
  does CRUD against a Cosmos container via the SDK (`Microsoft.Azure.Cosmos` 3.62),
  with `System.Text.Json` as the serializer.
- **Set the appropriate consistency level.** Not explicit — the account runs on
  Cosmos's default (Session) consistency; no code sets this deliberately.
- **Implement partitioning schemes.** `Models/TemperatureReading.cs` derives `Id`
  from the reading's date (`yyyy-MM-dd`), used as both item id and partition key
  (`/id`) — one partition per reading, documented in `README.md`'s
  [Data model](README.md#data-model-modelstemperaturereadingcs) section.
- **Create, read, update, and delete data using platform SDKs.** All four —
  `Data/ITemperatureRepository.cs` defines the contract, `CosmosTemperatureRepository`
  implements it, exercised from the Add/Edit/Delete pages in `Components/Pages/`.
- **Develop solutions that use Blob Storage.** Not covered — no Blob Storage in this
  project.

## Implement Azure security

- **Implement user authentication and authorization.** Azure App Service Easy Auth
  (Google as the identity provider) authenticates at the platform edge;
  `Auth/EasyAuthMiddleware.cs` reads the resulting `X-MS-CLIENT-PRINCIPAL` header into
  `HttpContext.User`. Authorization is role-based: `Auth/AuthOptions.cs` maps an
  allow-list of emails to an `Editor` role, enforced both in the UI
  (`AuthorizeView Roles="Editor"`) and server-side in the Add/Edit/Delete handlers —
  see [Access control](README.md#access-control).
- **Implement secure, cloud-native apps.** App Service runs with a **system-assigned
  managed identity**. Cosmos DB access uses that identity via Entra ID/RBAC
  (`DefaultAzureCredential` in `Program.cs`) instead of an account-key connection
  string — the Cosmos account has no key-based auth in use. The Google OAuth client
  secret lives in Key Vault (`kv-watertemperatures`, RBAC-authorization mode) and is
  read via an App Service **Key Vault reference**
  (`@Microsoft.KeyVault(SecretUri=...)`); the managed identity holds the
  **Key Vault Secrets User** role on the vault, and a separate **Cosmos DB Built-in
  Data Contributor** data-plane role for Cosmos. Full detail in the README's
  [Azure resources](README.md#azure-resources) section.

## Monitor, troubleshoot, and optimize Azure solutions

- **Implement Application Insights.** Wired via `Microsoft.ApplicationInsights.AspNetCore`
  and `AddApplicationInsightsTelemetry()` in `Program.cs`, gated on
  `APPLICATIONINSIGHTS_CONNECTION_STRING` being set (unset locally, so local runs
  don't send telemetry). Backed by a workspace-based resource
  (`appi-watertemperatures` + Log Analytics workspace `law-watertemperatures`).
  Verified live via `az monitor app-insights query` showing real requests.
- **Configure a diagnostic setting / implement caching / CDN.** Not covered — no
  Redis cache, no CDN, no custom diagnostic settings beyond the App Insights default.

## Connect to and consume Azure services and third-party services

- Not covered yet. No API Management, Event Grid, Event Hubs, Service Bus, or Logic
  Apps in this project — there's no cross-service messaging or API gateway need at
  this scale.

## What's missing, and why it's next

The Functions/serverless domain is the biggest gap: nothing here exercises triggers
or bindings. The natural fit for this project is a timer-triggered Function that
pulls a public water-temperature/weather feed and writes new readings into Cosmos on
a schedule — planned as a later addition rather than bolted on for coverage's sake.
