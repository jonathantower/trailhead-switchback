using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// Classification rule per user. PartitionKey = userId, RowKey = ruleId (GUID).
/// Name is unique per user. Order determines evaluation priority.
/// </summary>
public class RuleEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
}
