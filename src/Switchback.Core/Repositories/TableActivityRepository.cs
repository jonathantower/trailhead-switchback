using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableActivityRepository : IActivityRepository
{
    private readonly TableClient _table;

    public TableActivityRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableActivity);
    }

    public async Task<IReadOnlyList<ActivityEntity>> GetRecentAsync(string userId, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        if (count <= 0) return Array.Empty<ActivityEntity>();
        var list = new List<ActivityEntity>();
        var safeUserId = TableStorageConstants.EscapeODataString(userId);
        await foreach (var entity in _table.QueryAsync<ActivityEntity>(
            filter: $"PartitionKey eq '{safeUserId}'",
            maxPerPage: count,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            list.Add(entity);
            if (list.Count >= count) break;
        }
        return list;
    }

    public async Task AddWithCapAsync(ActivityEntity entity, int maxPerUser, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _table.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);

        var all = new List<ActivityEntity>();
        var safePartitionKey = TableStorageConstants.EscapeODataString(entity.PartitionKey);
        await foreach (var e in _table.QueryAsync<ActivityEntity>(
            filter: $"PartitionKey eq '{safePartitionKey}'",
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            all.Add(e);
        }
        if (all.Count <= maxPerUser) return;

        var toDelete = all.OrderByDescending(e => e.RowKey).Skip(maxPerUser).ToList();
        foreach (var e in toDelete)
        {
            await _table.DeleteEntityAsync(e.PartitionKey, e.RowKey, e.ETag, cancellationToken).ConfigureAwait(false);
        }
    }
}
