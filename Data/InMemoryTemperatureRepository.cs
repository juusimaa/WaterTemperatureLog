using System.Collections.Concurrent;
using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>
/// Thread-safe in-memory store for local development. Data is lost on restart.
/// Seeded with the historical readings exported from Numbers so the UI has real data to show.
/// </summary>
public class InMemoryTemperatureRepository : ITemperatureRepository
{
    private readonly ConcurrentDictionary<string, TemperatureReading> _store = new();

    /// <summary>
    /// Historical measurements (date, °C) exported from Numbers. Note: the source row
    /// "20.7.2032" is a data-entry typo for 2021 and is seeded as such.
    /// </summary>
    private static readonly (int Year, int Month, int Day, double Celsius)[] SeedData =
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

    public InMemoryTemperatureRepository()
    {
        foreach (var (year, month, day, celsius) in SeedData)
        {
            Seed(new TemperatureReading { MeasuredOn = new DateOnly(year, month, day), Celsius = celsius });
        }
    }

    private void Seed(TemperatureReading r) => _store[r.Id] = r;

    public Task AddAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        _store[reading.Id] = reading;
        return Task.CompletedTask;
    }

    public Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var reading);
        return Task.FromResult(reading);
    }

    public Task UpdateAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        _store[reading.Id] = reading;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default)
    {
        IReadOnlyList<TemperatureReading> result = _store.Values
            .OrderByDescending(r => r.MeasuredOn)
            .Take(take)
            .ToList();
        return Task.FromResult(result);
    }
}
