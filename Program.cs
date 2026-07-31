using System.Text.Json;
using ApexCharts;
using Azure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Localization;
using Microsoft.Azure.Cosmos;
using WaterTemperatures.Auth;
using WaterTemperatures.Components;
using WaterTemperatures.Data;
using WaterTemperatures.Resources;

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

// Storage: use Cosmos DB when an account endpoint is configured (production, or a
// developer who has opted in locally); otherwise fall back to the in-memory seed
// data for local dev. Auth to Cosmos is Entra ID (RBAC) via DefaultAzureCredential
// — no account key anywhere: in Azure it resolves to the App Service's managed
// identity, locally it falls back to the developer's own `az login` session.
var cosmosAccountEndpoint = builder.Configuration["Cosmos:AccountEndpoint"];
var cosmosDatabase = builder.Configuration["Cosmos:Database"] ?? "WaterTemperatures";
var cosmosContainer = builder.Configuration["Cosmos:Container"] ?? "Readings";

if (string.IsNullOrWhiteSpace(cosmosAccountEndpoint))
{
    builder.Services.AddSingleton<ITemperatureRepository, InMemoryTemperatureRepository>();
}
else
{
    builder.Services.AddSingleton(_ => new CosmosClient(cosmosAccountEndpoint, new DefaultAzureCredential(), new CosmosClientOptions
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

// Application Insights. Only wired up when a connection string is configured — in
// Azure it's an App Service setting; unlike Cosmos above, calling
// AddApplicationInsightsTelemetry() with no connection string throws at startup
// (the OpenTelemetry exporter it registers requires one), so it isn't safe to call
// unconditionally the way the Cosmos client is.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// Charting (Blazor-ApexCharts).
builder.Services.AddApexCharts();

// Finnish/English UI. Finnish is first in SupportedCultures and so the default:
// the readings are from Torniojoki at Jarhoinen, so the audience is local first.
// A visitor whose browser asks for English still gets English, and the header's
// language links override either choice via the culture cookie.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(AppText.SupportedCultures[0])
        .AddSupportedCultures(AppText.SupportedCultures)
        .AddSupportedUICultures(AppText.SupportedCultures);

    // Cookie first (an explicit choice wins), then Accept-Language. Dropping the
    // query-string provider keeps ?culture= from being a second, unpersisted way
    // to switch that the header links would then disagree with.
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});

var app = builder.Build();

// Ensure the Cosmos database and container exist before serving requests. Safe to
// run every startup; it is a no-op once they exist. Partition key is the item id.
if (!string.IsNullOrWhiteSpace(cosmosAccountEndpoint))
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

// Sets CurrentCulture/CurrentUICulture for the request. Must run before components
// render — and before the Blazor circuit is established, since the circuit captures
// the culture of the request that created it.
app.UseRequestLocalization();

// Translate the Easy Auth identity (or the dev fallback) into HttpContext.User before
// components render, so AuthorizeView and the editor checks see the signed-in user.
app.UseMiddleware<EasyAuthMiddleware>();

app.UseAntiforgery();

app.MapStaticAssets();

// Language switch, hit by the header links as a full page load rather than
// enhanced navigation: the culture is fixed for the lifetime of a Blazor circuit,
// so the circuit has to be rebuilt under the new one.
app.MapGet("/culture", (HttpContext http, string culture, string? redirect) =>
{
    if (!AppText.SupportedCultures.Contains(culture))
    {
        culture = AppText.SupportedCultures[0];
    }

    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            SameSite = SameSiteMode.Lax,
            // A language preference is not consent-gated, so it survives a
            // non-essential-cookie policy.
            IsEssential = true,
        });

    // Only ever bounce back into this site. A caller-supplied absolute URL — or a
    // protocol-relative "//host" or backslash variant, both of which count as
    // relative Uris — would otherwise make this an open redirect.
    var isLocal = !string.IsNullOrEmpty(redirect)
        && redirect.StartsWith('/')
        && !redirect.StartsWith("//")
        && !redirect.StartsWith("/\\");

    return Results.LocalRedirect(isLocal ? redirect! : "/");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
