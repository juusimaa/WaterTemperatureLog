using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace WaterTemperatures.Resources;

/// <summary>
/// Central place for user-facing text and location constants.
///
/// Text is looked up per request from AppText.resx (English, the neutral
/// fallback) and AppText.fi.resx (Finnish), keyed on
/// <see cref="CultureInfo.CurrentUICulture"/> — which
/// <c>UseRequestLocalization</c> sets from the culture cookie or the browser's
/// Accept-Language header. Call sites stay <c>AppText.Foo</c> rather than an
/// injected <c>IStringLocalizer</c>, so components need no extra plumbing and
/// static, interactive and prerendered markup all read the same way.
/// </summary>
public static class AppText
{
    /// <summary>The cultures the UI is translated into. First entry is the default.</summary>
    public static readonly string[] SupportedCultures = ["fi", "en"];

    private static readonly ResourceManager Strings =
        new("WaterTemperatures.Resources.AppText", typeof(AppText).Assembly);

    // Location. Proper nouns, so they are the same in both languages and stay
    // constants.
    public const string RiverName = "Torniojoki";
    public const string VillageName = "Jarhoinen";

    // The deployed origin, for tags that need an absolute URL (canonical, Open
    // Graph). One constant rather than reading the request, since the app has
    // exactly one production host and social crawlers do not send it anyway.
    public const string SiteUrl = "https://watertemperatures-jouni.azurewebsites.net";

    // Branding
    public static string AppName => Get();

    // Just the two proper nouns, so — unlike AppName — this needs no
    // translation and stays identical in both languages.
    public static string Subtitle => $"{RiverName}, {VillageName}";

    public static string MetaDescription => Get();

    // Header: language and sign-in
    public static string Language => Get();
    public static string LogOut => Get();
    public static string LogIn => Get();
    public static string LogInToEdit => Get();

    // Home: toolbar and view states
    public static string AddMeasurement => Get();
    public static string ViewToggle => Get();
    public static string ViewList => Get();
    public static string ViewGraph => Get();
    public static string Loading => Get();
    public static string NoReadingsYet => Get();
    public static string AddTheFirstOne => Get();

    // Home: summary stats
    public static string StatLatest => Get();
    public static string StatAllTimeHigh => Get();
    public static string StatAllTimeLow => Get();

    // Home: readings table
    public static string Date => Get();
    public static string Note => Get();
    public static string By => Get();
    public static string Edit => Get();
    public static string Unknown => Get();
    public static string AddedByOn(string editor, string when) => Format(nameof(AddedByOn), editor, when);
    public static string EditedByOn(string editor, string when) => Format(nameof(EditedByOn), editor, when);

    // Home: graph filter
    public static string AllYears => Get();
    public static string CompareYears => Get();
    public static string SelectYear => Get();
    public static string FilterByYear => Get();

    // Home: chart
    public static string ChartTitle => Get();
    public static string ChartTitleForYear(int year) => Format(nameof(ChartTitleForYear), year);
    public static string ChartTitleMonthlyAverage => Get();
    public static string SeriesTemperature => Get();

    // Chart toolbar tooltips (ApexCharts)
    public static string ChartMenu => Get();
    public static string ChartPan => Get();
    public static string ChartReset => Get();
    public static string ChartSelection => Get();
    public static string ChartSelectionZoom => Get();
    public static string ChartZoomIn => Get();
    public static string ChartZoomOut => Get();
    public static string ChartExportToPng => Get();
    public static string ChartExportToSvg => Get();
    public static string ChartExportToCsv => Get();

    // Add page
    public static string AddAReading => Get();
    public static string DuplicateReadingPrefix => Get();
    public static string DuplicateReadingSuffix(string celsius) => Format(nameof(DuplicateReadingSuffix), celsius);
    public static string EditThatReading => Get();
    public static string PickAnotherDate => Get();
    public static string SaveFailed => Get();
    public static string NeedEditorToAdd => Get();
    public static string Back => Get();

    // Edit page
    public static string EditReading => Get();
    public static string ReadingNotFound => Get();
    public static string DeleteConfirmPrefix => Get();
    public static string DeleteConfirmSuffix(string celsius) => Format(nameof(DeleteConfirmSuffix), celsius);
    public static string YesDeleteIt => Get();
    public static string Delete => Get();
    public static string NeedEditorToChange => Get();
    public static string UpdateFailedDeleted => Get();
    public static string AlreadyDeleted => Get();

    // Reading form
    public static string FieldTemperature => Get();
    public static string FieldNote => Get();
    public static string DateIsIdentityHelp => Get();
    public static string Save => Get();
    public static string Update => Get();
    public static string Cancel => Get();

    // Validation. Read by DataAnnotations through ErrorMessageResourceType, which
    // requires exactly this shape: a public static string property.
    public static string ValidationDateInFuture => Get();
    public static string ValidationTemperatureRange => Get();
    public static string ValidationTemperatureRequired => Get();

    // Not found / error pages
    public static string NotFoundTitle => Get();
    public static string NotFoundBody => Get();
    public static string ErrorTitle => Get();
    public static string ErrorBody => Get();
    public static string RequestId => Get();

    // Unhandled error banner
    public static string UnhandledError => Get();
    public static string Reload => Get();

    // Reconnect modal
    public static string Rejoining => Get();
    public static string RejoinRetryPrefix => Get();
    public static string RejoinRetrySuffix => Get();
    public static string RejoinFailed => Get();
    public static string RetryOrReload => Get();
    public static string Retry => Get();
    public static string SessionPaused => Get();
    public static string Resume => Get();
    public static string ResumeFailed => Get();
    public static string PleaseReload => Get();

    // Footer
    public static string FooterMethodNote => Get();

    // No translatable words beyond AppName, so this is composed directly
    // rather than round-tripped through the resx files.
    public static string FooterCopyright => $"© {DateTime.UtcNow.Year} Jouni Uusimaa — {AppName}";

    /// <summary>
    /// The resource whose key matches the calling member. Falling back to the key
    /// keeps a missing translation visible in the UI instead of throwing.
    /// </summary>
    private static string Get([CallerMemberName] string key = "") =>
        Strings.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>
    /// Composite-format resource. The key is passed explicitly rather than via
    /// CallerMemberName: with an optional trailing string parameter, a two-string
    /// call would bind its second argument to the key instead.
    /// </summary>
    private static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
