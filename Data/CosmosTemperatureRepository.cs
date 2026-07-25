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

    public async Task AddAsync(TemperatureReading reading, CancellationToken ct = default)
        => await _container.CreateItemAsync(reading, new PartitionKey(reading.Id), cancellationToken: ct);

    public async Task<TemperatureReading?> GetByIdAsync(string id, CancellationToken ct = default)
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

    public async Task UpdateAsync(TemperatureReading reading, CancellationToken ct = default)
        => await _container.ReplaceItemAsync(reading, reading.Id, new PartitionKey(reading.Id), cancellationToken: ct);

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _container.DeleteItemAsync<TemperatureReading>(id, new PartitionKey(id), cancellationToken: ct);
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // No-op if it does not exist, matching the interface contract.
        }
    }

    public async Task<IReadOnlyList<TemperatureReading>> GetRecentAsync(int take = 100, CancellationToken ct = default)
    {
        // MeasuredOn serializes as an ISO date string ("2026-07-16"), so lexical
        // descending order is the same as chronological newest-first order.
        var query = new QueryDefinition(
                "SELECT * FROM c ORDER BY c.MeasuredOn DESC OFFSET 0 LIMIT @take")
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
