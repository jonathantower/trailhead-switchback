using Azure;
using Azure.Data.Tables;

namespace Switchback.Core.Entities;

/// <summary>
/// OAuth connection per user and provider. PartitionKey = userId, RowKey = provider (Gmail | M365).
/// Tokens are stored encrypted (envelope encryption via Key Vault).
/// </summary>
public class ProviderConnectionEntity : ITableEntity
{
    public const string ProviderGmail = "Gmail";
    public const string ProviderM365 = "M365";

    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>Encrypted access token (envelope encryption), base64-encoded for table storage.</summary>
    public string? EncryptedAccessTokenBase64 { get; set; }

    /// <summary>Encrypted refresh token, base64-encoded for table storage.</summary>
    public string? EncryptedRefreshTokenBase64 { get; set; }

    /// <summary>When the access token expires (UTC).</summary>
    public DateTimeOffset? TokenExpiresAt { get; set; }

    /// <summary>When the account was connected.</summary>
    public DateTimeOffset? ConnectedAt { get; set; }

    /// <summary>User's email address for this provider (e.g. for Gmail push lookup).</summary>
    public string? EmailAddress { get; set; }

    /// <summary>Gmail watch expiration (epoch milliseconds). When to renew watch. Null for M365 or when watch not set.</summary>
    public long? GmailWatchExpiresAtMs { get; set; }
}
