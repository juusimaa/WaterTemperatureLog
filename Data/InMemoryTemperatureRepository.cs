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

    // Mirrors the Cosmos behaviour deliberately, so a duplicate date and a revived
    // soft-deleted date behave the same locally as they do in production.
    public Task<AddResult> AddAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        if (_store.TryAdd(reading.Id, reading))
        {
            return Task.FromResult(AddResult.Added);
        }

        if (!_store.TryGetValue(reading.Id, out var existing) || !existing.IsDeleted)
        {
            return Task.FromResult(AddResult.DuplicateDate);
        }

        // That day holds a deleted reading — revive it with the new values.
        _store.TryUpdate(reading.Id, reading, existing);
        return Task.FromResult(AddResult.Added);
    }

    public Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var reading);
        return Task.FromResult(reading is { IsDeleted: false } ? reading : null);
    }

    public Task<bool> UpdateAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(reading.Id, out var existing) || existing.IsDeleted)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_store.TryUpdate(reading.Id, reading, existing));
    }

    public Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var existing) || existing.IsDeleted)
        {
            return Task.FromResult(false);
        }

        existing.IsDeleted = true;
        existing.DeletedBy = deletedBy;
        existing.DeletedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default)
    {
        IReadOnlyList<TemperatureReading> result = _store.Values
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.MeasuredOn)
            .Take(take)
            .ToList();
        return Task.FromResult(result);
    }
}
