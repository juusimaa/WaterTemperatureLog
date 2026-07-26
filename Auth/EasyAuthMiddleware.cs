using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace WaterTemperatures.Auth;

/// <summary>
/// Bridges Azure App Service Easy Auth into ASP.NET Core's <see cref="ClaimsPrincipal"/>.
/// Easy Auth authenticates the user at the platform edge and forwards the signed-in
/// principal in the <c>X-MS-CLIENT-PRINCIPAL</c> header; this middleware reads that,
/// sets <see cref="HttpContext.User"/>, and adds the Editor role for allow-listed emails.
/// Locally (Development), where Easy Auth is not present, an optional dev email is used
/// so add/edit can still be tested.
/// </summary>
public class EasyAuthMiddleware(RequestDelegate next)
{
    // Claim types Easy Auth may use for the email, in order of preference.
    private static readonly string[] EmailClaimTypes =
    [
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
        "emails",
        "email",
        "preferred_username",
        ClaimTypes.Name,
        "name",
    ];

    public async Task InvokeAsync(HttpContext context, IOptions<AuthOptions> options, IHostEnvironment env)
    {
        var auth = options.Value;

        var email = ReadEasyAuthEmail(context);
        if (email is null && env.IsDevelopment())
        {
            // No Easy Auth in front of us locally — fall back to the configured dev editor.
            email = auth.DevAutoLoginEmail;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, email),
                new(ClaimTypes.Email, email),
            };
            if (auth.IsEditor(email))
            {
                claims.Add(new Claim(ClaimTypes.Role, AuthOptions.EditorRole));
            }

            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, authenticationType: "EasyAuth",
                    nameType: ClaimTypes.Name, roleType: ClaimTypes.Role));
        }

        await next(context);
    }

    private static string? ReadEasyAuthEmail(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var header) ||
            header.Count == 0 || string.IsNullOrEmpty(header[0]))
        {
            // Fall back to the simpler name header Easy Auth also injects.
            if (context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-NAME", out var name) &&
                name.Count > 0 && !string.IsNullOrEmpty(name[0]))
            {
                return name[0];
            }
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(header[0]!));
            var principal = JsonSerializer.Deserialize<EasyAuthPrincipal>(json);
            if (principal?.Claims is null)
            {
                return null;
            }

            foreach (var type in EmailClaimTypes)
            {
                var value = principal.Claims
                    .FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Malformed header — treat the request as anonymous.
        }

        return null;
    }

    private sealed class EasyAuthPrincipal
    {
        [JsonPropertyName("claims")]
        public List<EasyAuthClaim>? Claims { get; set; }
    }

    private sealed class EasyAuthClaim
    {
        [JsonPropertyName("typ")]
        public string? Type { get; set; }

        [JsonPropertyName("val")]
        public string? Value { get; set; }
    }
}
