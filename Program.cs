using System.Text.Json;
using ApexCharts;
using Microsoft.Azure.Cosmos;
using WaterTemperatures.Components;
using WaterTemperatures.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Storage: use Cosmos DB when a connection string is configured (production, or a
// local emulator); otherwise fall back to the in-memory seed data for local dev.
var cosmosConnectionString = builder.Configuration["Cosmos:ConnectionString"];
var cosmosDatabase = builder.Configuration["Cosmos:Database"] ?? "WaterTemperatures";
var cosmosContainer = builder.Configuration["Cosmos:Container"] ?? "Readings";

if (string.IsNullOrWhiteSpace(cosmosConnectionString))
{
    builder.Services.AddSingleton<ITemperatureRepository, InMemoryTemperatureRepository>();
}
else
{
    builder.Services.AddSingleton(_ => new CosmosClient(cosmosConnectionString, new CosmosClientOptions
    {
        // Use System.Text.Json so DateOnly and the [JsonPropertyName] attributes on the
        // model are honored, and property names stay PascalCase to match the queries.
        UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.General),
    }));

    builder.Services.AddSingleton<ITemperatureRepository>(sp =>
    {
        var container = sp.GetRequiredService<CosmosClient>().GetContainer(cosmosDatabase, cosmosContainer);
        return new CosmosTemperatureRepository(container);
    });
}

// Charting (Blazor-ApexCharts).
builder.Services.AddApexCharts();

var app = builder.Build();

// Ensure the Cosmos database and container exist before serving requests. Safe to
// run every startup; it is a no-op once they exist. Partition key is the item id.
if (!string.IsNullOrWhiteSpace(cosmosConnectionString))
{
    var cosmosClient = app.Services.GetRequiredService<CosmosClient>();
    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDatabase);
    await database.Database.CreateContainerIfNotExistsAsync(cosmosContainer, "/id");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
