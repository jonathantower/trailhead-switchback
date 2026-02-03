using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Functions.Services;

public sealed class ProviderConnectionAccessTokenProvider : IAccessTokenProvider
{
    private const string GmailTokenUrl = "https://oauth2.googleapis.com/token";

    private readonly IProviderConnectionRepository _connections;
    private readonly IEncryptionService _encryption;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public ProviderConnectionAccessTokenProvider(
        IProviderConnectionRepository connections,
        IEncryptionService encryption,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<string?> GetAccessTokenAsync(string userId, string provider, CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(userId, provider, cancellationToken).ConfigureAwait(false);
        if (connection == null) return null;

        var accessToken = await DecryptTokenAsync(connection.EncryptedAccessTokenBase64, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(accessToken)) return null;

        var expiresAt = connection.TokenExpiresAt ?? DateTimeOffset.MinValue;
        if (expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        var refreshToken = await DecryptTokenAsync(connection.EncryptedRefreshTokenBase64, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(refreshToken)) return accessToken;

        string? newAccessToken;
        int? expiresIn;
        string? newRefreshToken;

        if (provider == ProviderConnectionEntity.ProviderGmail)
            (newAccessToken, expiresIn, newRefreshToken) = await RefreshGmailAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        else if (provider == ProviderConnectionEntity.ProviderM365)
            (newAccessToken, expiresIn, newRefreshToken) = await RefreshM365Async(refreshToken, cancellationToken).ConfigureAwait(false);
        else
            return accessToken;

        if (string.IsNullOrEmpty(newAccessToken)) return accessToken;

        var accessBytes = Encoding.UTF8.GetBytes(newAccessToken);
        var encryptedAccess = await _encryption.EncryptAsync(accessBytes, cancellationToken).ConfigureAwait(false);
        connection.EncryptedAccessTokenBase64 = Convert.ToBase64String(encryptedAccess);
        connection.TokenExpiresAt = expiresIn.HasValue ? DateTimeOffset.UtcNow.AddSeconds(expiresIn.Value) : null;

        if (!string.IsNullOrEmpty(newRefreshToken))
        {
            var refreshBytes = Encoding.UTF8.GetBytes(newRefreshToken);
            var encryptedRefresh = await _encryption.EncryptAsync(refreshBytes, cancellationToken).ConfigureAwait(false);
            connection.EncryptedRefreshTokenBase64 = Convert.ToBase64String(encryptedRefresh);
        }

        await _connections.UpsertAsync(connection, cancellationToken).ConfigureAwait(false);
        return newAccessToken;
    }

    private async Task<string?> DecryptTokenAsync(string? encryptedBase64, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return null;
        try
        {
            var blob = Convert.FromBase64String(encryptedBase64);
            var plain = await _encryption.DecryptAsync(blob, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    private async Task<(string? AccessToken, int? ExpiresIn, string? RefreshToken)> RefreshGmailAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var clientId = _config["Gmail:ClientId"];
        var clientSecret = _config["Gmail:ClientSecret"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) return (null, null, null);

        using var http = _httpClientFactory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });
        var response = await http.PostAsync(GmailTokenUrl, form, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return (null, null, null);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : (int?)null;
        var newRefresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        return (access, expiresIn, newRefresh ?? refreshToken);
    }

    private async Task<(string? AccessToken, int? ExpiresIn, string? RefreshToken)> RefreshM365Async(string refreshToken, CancellationToken cancellationToken)
    {
        var clientId = _config["M365:ClientId"];
        var clientSecret = _config["M365:ClientSecret"];
        var tenantId = _config["M365:TenantId"] ?? "common";
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) return (null, null, null);

        var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        using var http = _httpClientFactory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });
        var response = await http.PostAsync(tokenUrl, form, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return (null, null, null);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : (int?)null;
        var newRefresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        return (access, expiresIn, newRefresh ?? refreshToken);
    }
}
