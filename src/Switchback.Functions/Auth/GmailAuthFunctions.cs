using System.Net;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Functions.Auth;

public class GmailAuthFunctions
{
    private const string GmailAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GmailTokenUrl = "https://oauth2.googleapis.com/token";
    private const string GmailScope = "https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/gmail.modify";

    private const string GmailProfileUrl = "https://gmail.googleapis.com/gmail/v1/users/me/profile";
    private readonly IConfiguration _config;
    private readonly IProviderConnectionRepository _connections;
    private readonly IUserEmailRepository _userEmail;
    private readonly IEncryptionService _encryption;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public GmailAuthFunctions(
        IConfiguration config,
        IProviderConnectionRepository connections,
        IUserEmailRepository userEmail,
        IEncryptionService encryption,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _connections = connections;
        _userEmail = userEmail;
        _encryption = encryption;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<GmailAuthFunctions>();
    }

    [Function("GmailAuthStart")]
    public async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/gmail/start")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        var clientId = _config["Gmail:ClientId"];
        var redirectUri = _config["Gmail:RedirectUri"];
        var webBaseUrl = _config["Auth:WebBaseUrl"] ?? "/";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Gmail OAuth not configured: ClientId or RedirectUri missing");
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync("OAuth not configured");
            return err;
        }

        var state = HttpUtility.UrlEncode(userId);
        var redirect = $"{GmailAuthUrl}?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(GmailScope)}&state={state}&access_type=offline&prompt=consent";
        var response = req.CreateResponse(HttpStatusCode.Redirect);
        response.Headers.Add("Location", redirect);
        return response;
    }

    [Function("GmailAuthCallback")]
    public async Task<HttpResponseData> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/gmail/callback")] HttpRequestData req)
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
        var clientId = _config["Gmail:ClientId"];
        var clientSecret = _config["Gmail:ClientSecret"];
        var redirectUri = _config["Gmail:RedirectUri"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Gmail OAuth not configured");
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            return err;
        }

        using var http = _httpClientFactory.CreateClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        var tokenResponse = await http.PostAsync(GmailTokenUrl, tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Gmail token exchange failed: {Status}", tokenResponse.StatusCode);
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
            RowKey = ProviderConnectionEntity.ProviderGmail,
            EncryptedAccessTokenBase64 = encryptedAccessBase64,
            EncryptedRefreshTokenBase64 = encryptedRefreshBase64,
            TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            ConnectedAt = DateTimeOffset.UtcNow
        };

        using (var httpForProfile = _httpClientFactory.CreateClient())
        {
            httpForProfile.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var profileResponse = await httpForProfile.GetAsync(GmailProfileUrl);
            if (profileResponse.IsSuccessStatusCode)
            {
                var profileJson = await profileResponse.Content.ReadAsStringAsync();
                var profileDoc = System.Text.Json.JsonDocument.Parse(profileJson);
                if (profileDoc.RootElement.TryGetProperty("emailAddress", out var emailEl))
                {
                    var emailAddress = emailEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(emailAddress))
                    {
                        entity.EmailAddress = emailAddress;
                        await _userEmail.UpsertAsync(new UserEmailEntity
                        {
                            PartitionKey = ProviderConnectionEntity.ProviderGmail,
                            RowKey = emailAddress,
                            UserId = userId
                        });
                    }
                }
            }
        }

        await _connections.UpsertAsync(entity);

        var redirectUrl = $"{webBaseUrl.TrimEnd('/')}/Connections?connected=Gmail";
        var response = req.CreateResponse(HttpStatusCode.Redirect);
        response.Headers.Add("Location", redirectUrl);
        return response;
    }
}
