using System.Collections.Concurrent;
using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>
/// Thread-safe in-memory store for local development. Data is lost on restart.
/// Seeded with a few sample readings so the UI has something to show.
/// </summary>
public class InMemoryTemperatureRepository : ITemperatureRepository
{
    private readonly ConcurrentDictionary<string, TemperatureReading> _store = new();

    public InMemoryTemperatureRepository()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        Seed(new TemperatureReading { MeasuredOn = today.AddDays(-2), Celsius = 17.5, Spot = "By the bridge" });
        Seed(new TemperatureReading { MeasuredOn = today.AddDays(-1), Celsius = 18.2, Spot = "By the bridge" });
        Seed(new TemperatureReading { MeasuredOn = today, Celsius = 19.1, Spot = "Swimming pier", Note = "Sunny" });
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
