using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// Tracks processed message IDs per provider for idempotency. PartitionKey = provider (Gmail | M365), RowKey = messageId.
/// Duplicate webhook deliveries for the same messageId are no-op.
/// </summary>
public class ProcessedMessageEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
