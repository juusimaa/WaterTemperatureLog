using System.Text.Json;
using ApexCharts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Azure.Cosmos;
using WaterTemperatures.Auth;
using WaterTemperatures.Components;
using WaterTemperatures.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Authentication/authorization. Sign-in is handled by App Service Easy Auth; here we
// only read the resulting identity (see EasyAuthMiddleware) and flow it to components.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

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

    // Populate the historical readings the first time the container is empty.
    var repository = (CosmosTemperatureRepository)app.Services.GetRequiredService<ITemperatureRepository>();
    await repository.SeedIfEmptyAsync(SeedData.Readings);

    // Containers seeded before the app tracked authorship have no creator on their
    // readings; credit those to the historical editor. No-op once it has run.
    var backfilled = await repository.BackfillMissingEditorAsync(SeedData.HistoricalEditor);
    if (backfilled > 0)
    {
        app.Logger.LogInformation(
            "Backfilled {Count} reading(s) with historical editor {Editor}.",
            backfilled, SeedData.HistoricalEditor);
    }
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

// Translate the Easy Auth identity (or the dev fallback) into HttpContext.User before
// components render, so AuthorizeView and the editor checks see the signed-in user.
app.UseMiddleware<EasyAuthMiddleware>();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
