using Azure;
using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableProcessedMessageRepository : IProcessedMessageRepository
{
    private readonly TableClient _table;

    public TableProcessedMessageRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableProcessedMessages);
    }

    public async Task<bool> ExistsAsync(string provider, string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(messageId);
        try
        {
            await _table.GetEntityAsync<ProcessedMessageEntity>(provider, messageId, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string provider, string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(messageId);
        var entity = new ProcessedMessageEntity
        {
            PartitionKey = provider,
            RowKey = messageId
        };
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }
}
