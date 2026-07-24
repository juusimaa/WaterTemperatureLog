using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>
/// Storage abstraction for temperature readings. The in-memory implementation is
/// used for local development; a Cosmos DB implementation swaps in for production
/// without any change to the UI.
/// </summary>
public interface ITemperatureRepository
{
    /// <summary>Add a new reading.</summary>
    Task AddAsync(TemperatureReading reading, CancellationToken ct = default);

    /// <summary>Get the most recent readings, newest first.</summary>
    Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default);
}
