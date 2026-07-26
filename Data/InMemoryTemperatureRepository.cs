using System.Collections.Concurrent;
using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>
/// Thread-safe in-memory store for local development. Data is lost on restart.
/// Seeded with the shared historical readings so the UI has real data to show.
/// </summary>
public class InMemoryTemperatureRepository : ITemperatureRepository
{
    private readonly ConcurrentDictionary<string, TemperatureReading> _store = new();

    public InMemoryTemperatureRepository()
    {
        foreach (var reading in SeedData.Readings)
        {
            _store[reading.Id] = reading;
        }
    }

    // Mirrors the Cosmos behaviour deliberately: insert-only, never an overwrite, so a
    // duplicate date fails the same way locally as it does in production.
    public Task<AddResult> AddAsync(TemperatureReading reading, CancellationToken ct = default)
        => Task.FromResult(_store.TryAdd(reading.Id, reading) ? AddResult.Added : AddResult.DuplicateDate);

    public Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var reading);
        return Task.FromResult(reading);
    }

    public Task<bool> UpdateAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(reading.Id, out var existing))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_store.TryUpdate(reading.Id, reading, existing));
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
