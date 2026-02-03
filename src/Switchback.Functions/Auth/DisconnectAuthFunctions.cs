using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;

namespace Switchback.Functions.Auth;

public class DisconnectAuthFunctions
{
    private readonly IConfiguration _config;
    private readonly IProviderConnectionRepository _connections;
    private readonly ILogger _logger;

    public DisconnectAuthFunctions(
        IConfiguration config,
        IProviderConnectionRepository connections,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _connections = connections;
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

        await _connections.DeleteAsync(userId, provider);

        var webBaseUrl = _config["Auth:WebBaseUrl"] ?? "/";
        var redirectUrl = $"{webBaseUrl.TrimEnd('/')}/Connections?disconnected={provider}";
        var response = req.CreateResponse(HttpStatusCode.Redirect);
        response.Headers.Add("Location", redirectUrl);
        return response;
    }
}
