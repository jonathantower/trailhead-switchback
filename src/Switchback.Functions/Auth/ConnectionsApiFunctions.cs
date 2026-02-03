using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;

namespace Switchback.Functions.Auth;

public class ConnectionsApiFunctions
{
    private readonly IProviderConnectionRepository _connections;
    private readonly ILogger _logger;

    public ConnectionsApiFunctions(
        IProviderConnectionRepository connections,
        ILoggerFactory loggerFactory)
    {
        _connections = connections;
        _logger = loggerFactory.CreateLogger<ConnectionsApiFunctions>();
    }

    [Function("ConnectionsList")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/connections")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        var list = await _connections.GetAllForUserAsync(userId);
        var dto = new
        {
            gmail = list.Any(c => c.RowKey == ProviderConnectionEntity.ProviderGmail),
            m365 = list.Any(c => c.RowKey == ProviderConnectionEntity.ProviderM365)
        };
        var json = JsonSerializer.Serialize(dto);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(json);
        return response;
    }
}
