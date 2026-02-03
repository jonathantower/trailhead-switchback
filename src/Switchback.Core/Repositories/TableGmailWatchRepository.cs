using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableGmailWatchRepository : IGmailWatchRepository
{
    private readonly TableClient _table;

    public TableGmailWatchRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableGmailWatch);
    }

    public async Task<GmailWatchEntity?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        try
        {
            var response = await _table.GetEntityAsync<GmailWatchEntity>(GmailWatchEntity.PartitionKeyValue, userId, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<GmailWatchEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<GmailWatchEntity>();
        await foreach (var entity in _table.QueryAsync<GmailWatchEntity>(
            filter: $"PartitionKey eq '{GmailWatchEntity.PartitionKeyValue}'",
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            list.Add(entity);
        }
        return list;
    }

    public async Task UpsertAsync(GmailWatchEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.PartitionKey = GmailWatchEntity.PartitionKeyValue;
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        await _table.DeleteEntityAsync(GmailWatchEntity.PartitionKeyValue, userId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
