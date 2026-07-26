using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>Outcome of an attempt to add a reading.</summary>
public enum AddResult
{
    /// <summary>The reading was stored.</summary>
    Added,

    /// <summary>A reading already exists for that date; nothing was written.</summary>
    DuplicateDate,
}

/// <summary>
/// Storage abstraction for temperature readings. The in-memory implementation is
/// used for local development; a Cosmos DB implementation swaps in for production
/// without any change to the UI.
/// </summary>
/// <remarks>
/// A reading's id is its date (see <see cref="TemperatureReading.IdFor"/>), so at most
/// one reading can exist per day. Implementations must report a same-day collision as
/// <see cref="AddResult.DuplicateDate"/> rather than overwriting or throwing.
/// </remarks>
public interface ITemperatureRepository
{
    /// <summary>
    /// Add a new reading. Returns <see cref="AddResult.DuplicateDate"/> without writing
    /// if a reading already exists for <see cref="TemperatureReading.MeasuredOn"/>.
    /// </summary>
    Task<AddResult> AddAsync(TemperatureReading reading, CancellationToken ct = default);

    /// <summary>Get a single reading by id, or null if it does not exist.</summary>
    Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Update an existing reading (matched by id, i.e. by date). Returns false if no
    /// reading exists for that date — e.g. it was deleted while the form was open.
    /// </summary>
    Task<bool> UpdateAsync(TemperatureReading reading, CancellationToken ct = default);

    /// <summary>Delete a reading by id. No-op if it does not exist.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Get the most recent readings, newest first.</summary>
    Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default);
}
