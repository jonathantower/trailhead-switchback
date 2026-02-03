namespace Switchback.Core.Services;

/// <summary>
/// Provides a valid OAuth access token for a user's provider connection, refreshing if expired.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>Gets a valid access token for the user's connection to the provider. Refreshes and saves if expired.</summary>
    Task<string?> GetAccessTokenAsync(string userId, string provider, CancellationToken cancellationToken = default);
}
