using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// Tracks Gmail watch expiration for renewal. PartitionKey = "watches", RowKey = userId.
/// Timer lists all and renews where ExpiresAtMs is within the next 24–48 hours.
/// </summary>
public class GmailWatchEntity : ITableEntity
{
    public const string PartitionKeyValue = "watches";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>Epoch milliseconds when the Gmail watch expires. Renew before this.</summary>
    public long ExpiresAtMs { get; set; }
}
