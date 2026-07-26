using System.Net;
using Microsoft.Azure.Cosmos;
using WaterTemperatures.Models;

namespace WaterTemperatures.Data;

/// <summary>
/// Azure Cosmos DB implementation of <see cref="ITemperatureRepository"/>, used in
/// production. Each reading lives in its own logical partition (partition key = the
/// item id), which keeps the id-only point reads and deletes cheap. The dataset is
/// small, so the cross-partition query in <see cref="GetRecentAsync"/> is inexpensive.
/// </summary>
public class CosmosTemperatureRepository : ITemperatureRepository
{
    private readonly Container _container;

    public CosmosTemperatureRepository(Container container) => _container = container;

    public async Task<AddResult> AddAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        try
        {
            await _container.CreateItemAsync(reading, new PartitionKey(reading.Id), cancellationToken: ct);
            return AddResult.Added;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.Conflict)
        {
            // The id is the date, so a 409 means that day is already taken — either by a
            // live reading (reject) or by a soft-deleted one (revive it with the new
            // values). Callers pre-check, but two editors saving at once still land here.
            var existing = await ReadRawAsync(reading.Id, ct);
            if (existing is null)
            {
                // Hard-deleted in the gap between the conflict and this read; retry once.
                await _container.CreateItemAsync(reading, new PartitionKey(reading.Id), cancellationToken: ct);
                return AddResult.Added;
            }

            if (!existing.IsDeleted)
            {
                return AddResult.DuplicateDate;
            }

            await _container.ReplaceItemAsync(
                reading, reading.Id, new PartitionKey(reading.Id), cancellationToken: ct);
            return AddResult.Added;
        }
    }

    public async Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var reading = await ReadRawAsync(id, ct);
        return reading is { IsDeleted: false } ? reading : null;
    }

    public async Task<bool> UpdateAsync(TemperatureReading reading, CancellationToken ct = default)
    {
        // Do not resurrect a deleted reading through the edit form: an update to a
        // deleted date must fail the same way an update to a missing one does.
        var existing = await ReadRawAsync(reading.Id, ct);
        if (existing is null || existing.IsDeleted)
        {
            return false;
        }

        try
        {
            await _container.ReplaceItemAsync(
                reading, reading.Id, new PartitionKey(reading.Id), cancellationToken: ct);
            return true;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Deleted between loading the form and saving it.
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken ct = default)
    {
        var reading = await ReadRawAsync(id, ct);
        if (reading is null || reading.IsDeleted)
        {
            return false;
        }

        reading.IsDeleted = true;
        reading.DeletedBy = deletedBy;
        reading.DeletedAt = DateTimeOffset.UtcNow;

        try
        {
            await _container.ReplaceItemAsync(reading, id, new PartitionKey(id), cancellationToken: ct);
            return true;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>Point-read an item including soft-deleted ones, or null if it is absent.</summary>
    private async Task<TemperatureReading?> ReadRawAsync(string id, CancellationToken ct)
    {
        try
        {
            var response = await _container.ReadItemAsync<TemperatureReading>(
                id, new PartitionKey(id), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Populates the container with the given readings, but only when it is empty, so a
    /// user's later edits or deletions are never overwritten on the next startup. The
    /// readings use deterministic ids, so an upsert also makes this safe to run twice.
    /// </summary>
    public async Task SeedIfEmptyAsync(IEnumerable<TemperatureReading> readings, CancellationToken ct = default)
    {
        var existing = 0;
        using var countIterator = _container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        while (countIterator.HasMoreResults)
        {
            foreach (var count in await countIterator.ReadNextAsync(ct))
            {
                existing += count;
            }
        }

        if (existing > 0)
        {
            return;
        }

        foreach (var reading in readings)
        {
            await _container.UpsertItemAsync(reading, new PartitionKey(reading.Id), cancellationToken: ct);
        }
    }

    /// <summary>
    /// One-time backfill for readings stored before the app recorded authorship: stamps
    /// <paramref name="editor"/> as the creator, dated to the measurement day. Idempotent —
    /// readings that already have a creator are left untouched, so it is safe to run on
    /// every startup and does no writes once the container has been backfilled.
    /// </summary>
    /// <returns>The number of readings updated.</returns>
    public async Task<int> BackfillMissingEditorAsync(string editor, CancellationToken ct = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE NOT IS_DEFINED(c.CreatedBy) OR IS_NULL(c.CreatedBy)");

        var stale = new List<TemperatureReading>();
        using var iterator = _container.GetItemQueryIterator<TemperatureReading>(query);
        while (iterator.HasMoreResults)
        {
            stale.AddRange(await iterator.ReadNextAsync(ct));
        }

        foreach (var reading in stale)
        {
            reading.CreatedBy = editor;
            if (reading.CreatedAt == default)
            {
                // The real recording time is unknown; use midnight UTC on the measured day.
                reading.CreatedAt = new DateTimeOffset(
                    reading.MeasuredOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            }

            await _container.ReplaceItemAsync(
                reading, reading.Id, new PartitionKey(reading.Id), cancellationToken: ct);
        }

        return stale.Count;
    }

    public async Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default)
    {
        // MeasuredOn serializes as an ISO date string ("2026-07-16"), so lexical
        // descending order is the same as chronological newest-first order.
        // IS_DEFINED guards readings written before soft delete existed.
        var query = new QueryDefinition(
                "SELECT * FROM c WHERE NOT IS_DEFINED(c.IsDeleted) OR c.IsDeleted = false " +
                "ORDER BY c.MeasuredOn DESC OFFSET 0 LIMIT @take")
            .WithParameter("@take", take);

        var results = new List<TemperatureReading>(take);
        using var iterator = _container.GetItemQueryIterator<TemperatureReading>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var reading in await iterator.ReadNextAsync(ct))
            {
                results.Add(reading);
            }
        }

        return results;
    }
}
