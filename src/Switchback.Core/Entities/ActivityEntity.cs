using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// Activity record per user. PartitionKey = userId, RowKey = reverse timestamp + messageId (newest first).
/// Capped at 50 per user; oldest deleted when inserting beyond cap.
/// </summary>
public class ActivityEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>When the email was processed (stored for display).</summary>
    public DateTimeOffset ProcessedAt { get; set; }

    public string Subject { get; set; } = "";
    /// <summary>Rule name that was applied, or "NONE".</summary>
    public string RuleApplied { get; set; } = "";
    public string Destination { get; set; } = "";
    /// <summary>Provider: Gmail | M365.</summary>
    public string Provider { get; set; } = "";
    public string MessageId { get; set; } = "";
}
