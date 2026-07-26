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
/// <para>
/// A reading's id is its date (see <see cref="TemperatureReading.IdFor"/>), so at most
/// one reading can exist per day. Implementations must report a same-day collision as
/// <see cref="AddResult.DuplicateDate"/> rather than overwriting or throwing.
/// </para>
/// <para>
/// Deletes are soft: the item stays in the store with
/// <see cref="TemperatureReading.IsDeleted"/> set, so nothing is ever lost to a mis-click.
/// Every read on this interface hides deleted readings, so callers can ignore the flag.
/// </para>
/// </remarks>
public interface ITemperatureRepository
{
    /// <summary>
    /// Add a new reading. Returns <see cref="AddResult.DuplicateDate"/> without writing
    /// if a live reading already exists for <see cref="TemperatureReading.MeasuredOn"/>.
    /// If that date holds a soft-deleted reading, it is revived with the new values and
    /// <see cref="AddResult.Added"/> is returned.
    /// </summary>
    Task<AddResult> AddAsync(TemperatureReading reading, CancellationToken ct = default);

    /// <summary>Get a single reading by id, or null if it does not exist or was deleted.</summary>
    Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Update an existing reading (matched by id, i.e. by date). Returns false if no live
    /// reading exists for that date — e.g. it was deleted while the form was open.
    /// </summary>
    Task<bool> UpdateAsync(TemperatureReading reading, CancellationToken ct = default);

    /// <summary>
    /// Soft-delete a reading by id, recording who deleted it. Returns false if no live
    /// reading exists for that date. The item is retained and can be restored by clearing
    /// <see cref="TemperatureReading.IsDeleted"/>, or by re-adding a reading for that day.
    /// </summary>
    Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken ct = default);

    /// <summary>Get the most recent readings, newest first. Excludes deleted readings.</summary>
    Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default);
}
