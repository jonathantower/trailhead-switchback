using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// User entity for sign-up/login. PartitionKey = "users", RowKey = username (unique).
/// Password stored hashed. UserId is the stable id used as PartitionKey in other tables.
/// </summary>
public class UserEntity : ITableEntity
{
    public const string PartitionKeyValue = "users";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>Stable user ID (GUID) used as PartitionKey in ProviderConnection, Rule, Activity.</summary>
    public string UserId { get; set; } = "";

    /// <summary>Hashed password (e.g. from ASP.NET Core Identity or BCrypt).</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>When the user was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
