using Azure;
using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableUserRepository : IUserRepository
{
    private readonly TableClient _table;

    public TableUserRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableUsers);
    }

    public async Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);
        try
        {
            var response = await _table.GetEntityAsync<UserEntity>(
                UserEntity.PartitionKeyValue,
                username,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<UserEntity?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var safeUserId = TableStorageConstants.EscapeODataString(userId);
        await foreach (var entity in _table.QueryAsync<UserEntity>(
            filter: $"PartitionKey eq '{UserEntity.PartitionKeyValue}' and UserId eq '{safeUserId}'",
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return entity;
        }
        return null;
    }

    public async Task UpsertAsync(UserEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }
}
