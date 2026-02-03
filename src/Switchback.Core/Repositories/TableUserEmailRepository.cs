using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableUserEmailRepository : IUserEmailRepository
{
    private readonly TableClient _table;

    public TableUserEmailRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableUserEmail);
    }

    public async Task<string?> GetUserIdAsync(string provider, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(email);
        var rowKey = email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(rowKey)) return null;
        try
        {
            var response = await _table.GetEntityAsync<UserEmailEntity>(provider, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value.UserId;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertAsync(UserEmailEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!string.IsNullOrEmpty(entity.RowKey))
            entity.RowKey = entity.RowKey.Trim().ToLowerInvariant();
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string provider, string email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(email);
        var rowKey = email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(rowKey)) return;
        await _table.DeleteEntityAsync(provider, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
