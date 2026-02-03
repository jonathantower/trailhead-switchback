using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// Maps provider email address to userId for push notification lookup.
/// PartitionKey = provider (Gmail | M365), RowKey = normalized email (lowercase).
/// </summary>
public class UserEmailEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId { get; set; } = "";
}
