using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Functions.Auth;

public class DisconnectAuthFunctions
{
    private const string GmailStopUrl = "https://gmail.googleapis.com/gmail/v1/users/me/stop";
    private readonly IConfiguration _config;
    private readonly IProviderConnectionRepository _connections;
    private readonly IUserEmailRepository _userEmail;
    private readonly IGmailWatchRepository _gmailWatch;
    private readonly IAccessTokenProvider _tokens;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public DisconnectAuthFunctions(
        IConfiguration config,
        IProviderConnectionRepository connections,
        IUserEmailRepository userEmail,
        IGmailWatchRepository gmailWatch,
        IAccessTokenProvider tokens,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _connections = connections;
        _userEmail = userEmail;
        _gmailWatch = gmailWatch;
        _tokens = tokens;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<DisconnectAuthFunctions>();
    }

    [Function("AuthDisconnect")]
    public async Task<HttpResponseData> Disconnect(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "auth/disconnect")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        var provider = req.Query["provider"];

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(provider))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId or provider");
            return bad;
        }

        if (provider != ProviderConnectionEntity.ProviderGmail && provider != ProviderConnectionEntity.ProviderM365)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid provider");
            return bad;
        }

        if (provider == ProviderConnectionEntity.ProviderGmail)
        {
            var accessToken = await _tokens.GetAccessTokenAsync(userId, provider);
            if (!string.IsNullOrEmpty(accessToken))
            {
                using var http = _httpClientFactory.CreateClient();
                using var stopReq = new HttpRequestMessage(HttpMethod.Post, GmailStopUrl);
                stopReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                _ = await http.SendAsync(stopReq);
            }
            await _gmailWatch.DeleteAsync(userId);
        }

        var connection = await _connections.GetAsync(userId, provider);
        if (connection?.EmailAddress != null)
            await _userEmail.DeleteAsync(provider, connection.EmailAddress);

        await _connections.DeleteAsync(userId, provider);

        var webBaseUrl = _config["Auth:WebBaseUrl"] ?? "/";
        var redirectUrl = $"{webBaseUrl.TrimEnd('/')}/Connections?disconnected={provider}";
        var response = req.CreateResponse(HttpStatusCode.Redirect);
        response.Headers.Add("Location", redirectUrl);
        return response;
    }
}
