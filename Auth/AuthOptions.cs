namespace WaterTemperatures.Auth;

/// <summary>
/// Authentication/authorization settings bound from the "Auth" configuration section.
/// The actual sign-in is handled by Azure App Service Easy Auth (provider-agnostic);
/// this app only decides who counts as an editor.
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Role name granted to allow-listed users. Used by <c>AuthorizeView Roles</c>.</summary>
    public const string EditorRole = "Editor";

    /// <summary>Email addresses allowed to add/edit/delete readings. Everyone else can only view.</summary>
    public List<string> Editors { get; set; } = [];

    /// <summary>
    /// Development-only convenience: when set, the app signs you in as this email locally
    /// (where Easy Auth does not run). Ignored outside the Development environment. Leave
    /// unset in production configuration.
    /// </summary>
    public string? DevAutoLoginEmail { get; set; }

    /// <summary>True if the given email is on the editor allow-list (case-insensitive).</summary>
    public bool IsEditor(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        Editors.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
}
