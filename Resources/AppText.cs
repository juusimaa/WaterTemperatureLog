namespace WaterTemperatures.Resources;

/// <summary>
/// Central place for user-facing text and location constants.
///
/// For now these are plain English constants. When Finnish/English support is
/// added, replace the string members with lookups via <c>IStringLocalizer</c>
/// backed by .resx resource files (AppText.fi.resx, AppText.en.resx) — the call
/// sites in the .razor pages stay the same shape.
/// </summary>
public static class AppText
{
    // Location
    public const string RiverName = "Torniojoki";
    public const string VillageName = "Jarhoinen";

    // Branding
    public const string AppName = "Water Temperature Log";
    public static string Subtitle => $"{RiverName} river at {VillageName} village.";
}
