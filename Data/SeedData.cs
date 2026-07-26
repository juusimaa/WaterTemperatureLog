using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>
/// Historical water-temperature measurements (date, °C) exported from Numbers. Shared
/// by the in-memory repository (as its starting data) and by the Cosmos seeding step
/// (to populate an empty container on first run). Ids are derived from the date so
/// seeding is idempotent — re-running never creates duplicates.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Attribution for the historical rows: they predate the app, and were all recorded
    /// and transcribed by Jouni, so they are credited to him rather than left blank.
    /// </summary>
    public const string HistoricalEditor = "jouni.uusimaa@gmail.com";

    /// <summary>
    /// Note: the source row "20.7.2032" is a data-entry typo for 2021 and is stored as such.
    /// </summary>
    private static readonly (int Year, int Month, int Day, decimal Celsius)[] Rows =
    [
        (2021, 6, 25, 17),
        (2021, 7, 13, 24),
        (2021, 7, 18, 19),
        (2021, 7, 19, 18),
        (2021, 7, 20, 17),
        (2021, 7, 22, 16),
        (2021, 7, 25, 17),
        (2021, 7, 26, 20),
        (2021, 7, 27, 23),
        (2022, 5, 29, 8),
        (2022, 6, 23, 14),
        (2022, 6, 24, 15),
        (2022, 6, 25, 16),
        (2022, 6, 26, 17),
        (2022, 7, 2, 23),
        (2022, 7, 3, 23),
        (2022, 7, 12, 20),
        (2022, 7, 15, 21),
        (2022, 8, 1, 17),
        (2023, 7, 5, 19),
        (2023, 7, 23, 15),
        (2023, 7, 28, 17),
        (2024, 6, 21, 16),
        (2024, 7, 10, 19),
        (2024, 7, 15, 20),
        (2024, 7, 17, 22),
        (2024, 7, 20, 23),
        (2025, 7, 10, 17),
        (2025, 7, 12, 19),
        (2025, 7, 13, 20),
        (2025, 7, 14, 22),
        (2025, 7, 19, 24),
        (2025, 7, 23, 26),
        (2025, 8, 4, 21),
        (2026, 5, 16, 7),
        (2026, 6, 19, 14),
        (2026, 7, 1, 17),
        (2026, 7, 16, 20),
    ];

    /// <summary>The historical readings as fresh <see cref="TemperatureReading"/> instances.</summary>
    public static IEnumerable<TemperatureReading> Readings =>
        Rows.Select(r => new TemperatureReading
        {
            // Id is derived from MeasuredOn, so it is not set here.
            MeasuredOn = new DateOnly(r.Year, r.Month, r.Day),
            Celsius = r.Celsius,
            CreatedBy = HistoricalEditor,
            // The real recording time is unknown, so use midnight UTC on the measurement
            // day: deterministic, so re-seeding produces byte-identical items.
            CreatedAt = new DateTimeOffset(new DateTime(r.Year, r.Month, r.Day), TimeSpan.Zero),
        });
}
