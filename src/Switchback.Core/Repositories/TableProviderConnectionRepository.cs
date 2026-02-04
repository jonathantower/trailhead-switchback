using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableProviderConnectionRepository : IProviderConnectionRepository
{
    private readonly TableClient _table;

    public TableProviderConnectionRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableProviderConnections);
    }

    public async Task<ProviderConnectionEntity?> GetAsync(string userId, string provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(provider);
        try
        {
            var response = await _table.GetEntityAsync<ProviderConnectionEntity>(userId, provider, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ProviderConnectionEntity>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var list = new List<ProviderConnectionEntity>();
        var safeUserId = TableStorageConstants.EscapeODataString(userId);
        await foreach (var entity in _table.QueryAsync<ProviderConnectionEntity>(
            filter: $"PartitionKey eq '{safeUserId}'",
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            list.Add(entity);
        }
        return list;
    }

    public async Task UpsertAsync(ProviderConnectionEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string userId, string provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(provider);
        await _table.DeleteEntityAsync(userId, provider, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
