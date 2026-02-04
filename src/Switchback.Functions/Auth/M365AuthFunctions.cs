using System.Net;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Functions.Auth;

public class M365AuthFunctions
{
    private static readonly string[] M365Scopes = ["https://graph.microsoft.com/Mail.Read", "https://graph.microsoft.com/Mail.ReadWrite", "offline_access"];

    private readonly IConfiguration _config;
    private readonly IProviderConnectionRepository _connections;
    private readonly IEncryptionService _encryption;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public M365AuthFunctions(
        IConfiguration config,
        IProviderConnectionRepository connections,
        IEncryptionService encryption,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _connections = connections;
        _encryption = encryption;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<M365AuthFunctions>();
    }

    [Function("M365AuthStart")]
    public async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/auth/m365/start")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        var clientId = _config["M365:ClientId"];
        var tenantId = _config["M365:TenantId"] ?? "common";
        var redirectUri = _config["M365:RedirectUri"];
        var clientSecret = _config["M365:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogError("M365 OAuth not configured");
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync("OAuth not configured");
            return err;
        }

        var app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithRedirectUri(redirectUri)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .Build();

        var state = HttpUtility.UrlEncode(userId);
        var builder = app.GetAuthorizationRequestUrl(M365Scopes).WithExtraQueryParameters(new Dictionary<string, string> { ["state"] = state });
        var authUri = await builder.ExecuteAsync();

        var response = req.CreateResponse(HttpStatusCode.Redirect);
        response.Headers.Add("Location", authUri.AbsoluteUri);
        return response;
    }

    [Function("M365AuthCallback")]
    public async Task<HttpResponseData> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/auth/m365/callback")] HttpRequestData req)
    {
        var code = req.Query["code"];
        var state = req.Query["state"];
        var webBaseUrl = _config["Auth:WebBaseUrl"] ?? "/";

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing code or state");
            return bad;
        }

        var userId = HttpUtility.UrlDecode(state);
        var clientId = _config["M365:ClientId"];
        var tenantId = _config["M365:TenantId"] ?? "common";
        var redirectUri = _config["M365:RedirectUri"];
        var clientSecret = _config["M365:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogError("M365 OAuth not configured");
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            return err;
        }

        var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        using var http = _httpClientFactory.CreateClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        var tokenResponse = await http.PostAsync(tokenUrl, tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("M365 token exchange failed: {Status}", tokenResponse.StatusCode);
            var err = req.CreateResponse(HttpStatusCode.BadRequest);
            await err.WriteStringAsync("Token exchange failed");
            return err;
        }

        var json = await tokenResponse.Content.ReadAsStringAsync();
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600;

        if (string.IsNullOrEmpty(accessToken))
        {
            var err = req.CreateResponse(HttpStatusCode.BadRequest);
            await err.WriteStringAsync("No access token in response");
            return err;
        }

        var accessBytes = System.Text.Encoding.UTF8.GetBytes(accessToken);
        var encryptedAccess = await _encryption.EncryptAsync(accessBytes);
        var encryptedAccessBase64 = Convert.ToBase64String(encryptedAccess);

        string? encryptedRefreshBase64 = null;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var refreshBytes = System.Text.Encoding.UTF8.GetBytes(refreshToken);
            var encryptedRefresh = await _encryption.EncryptAsync(refreshBytes);
            encryptedRefreshBase64 = Convert.ToBase64String(encryptedRefresh);
        }

        var entity = new ProviderConnectionEntity
        {
            PartitionKey = userId,
            RowKey = ProviderConnectionEntity.ProviderM365,
            EncryptedAccessTokenBase64 = encryptedAccessBase64,
            EncryptedRefreshTokenBase64 = encryptedRefreshBase64,
            TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            ConnectedAt = DateTimeOffset.UtcNow
        };
        await _connections.UpsertAsync(entity);

        var redirectUrl = $"{webBaseUrl.TrimEnd('/')}/Connections?connected=M365";
        var response = req.CreateResponse(HttpStatusCode.Redirect);
        response.Headers.Add("Location", redirectUrl);
        return response;
    }
}
