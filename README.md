# Water Temperatures — Torniojoki, Järhöinen

Blazor Web App (.NET 10) to add and view water-temperature readings from the
Torniojoki river at Järhöinen village.

## Stack

- **Frontend + backend**: Blazor Web App, Interactive Server render mode (all C#).
- **Storage**: repository abstraction (`Data/ITemperatureRepository.cs`).
  - Local dev: `InMemoryTemperatureRepository` (registered in `Program.cs`). Data is lost on restart; seeded with sample readings.
  - Production: a Cosmos DB implementation drops in with no UI changes (see below).
- **Hosting target**: Azure App Service.

## Run locally

```bash
dotnet run
```

Then open the URL shown in the console. Pages:
- `/` — view readings (newest first).
- `/add` — add a reading.

## Project layout

- `Models/TemperatureReading.cs` — the domain model (id, measured-at, °C, spot, note).
- `Data/ITemperatureRepository.cs` — storage contract.
- `Data/InMemoryTemperatureRepository.cs` — dev implementation.
- `Components/Pages/Home.razor` — readings list.
- `Components/Pages/AddReading.razor` — add form with validation.

## Next steps toward production

1. **Add Cosmos DB support**: implement `CosmosTemperatureRepository : ITemperatureRepository`
   using the `Microsoft.Azure.Cosmos` package, partition on e.g. `/spot` or a fixed
   location key, and register it in `Program.cs` behind an environment check.
2. **Provision Azure** (do this last, when the app is ready). Sketch:

   ```bash
   RG=rg-watertemps
   LOC=swedencentral
   az group create -n $RG -l $LOC

   # Cosmos DB (serverless) + database + container
   az cosmosdb create -n <cosmos-account> -g $RG --capabilities EnableServerless
   az cosmosdb sql database create -a <cosmos-account> -g $RG -n WaterTemps
   az cosmosdb sql container create -a <cosmos-account> -g $RG -d WaterTemps \
     -n Readings --partition-key-path /spot

   # App Service (Linux) to host the Blazor app
   az appservice plan create -n plan-watertemps -g $RG --is-linux --sku B1
   az webapp create -n <app-name> -g $RG -p plan-watertemps --runtime "DOTNETCORE:10.0"
   ```

3. **Wire config**: put the Cosmos connection string in App Service configuration
   (or use Managed Identity + `DefaultAzureCredential`, preferred — no secrets in config).
4. **Deploy**: `az webapp deploy` / `dotnet publish` + zip deploy, or GitHub Actions.
