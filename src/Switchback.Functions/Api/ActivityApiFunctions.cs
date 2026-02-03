using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Switchback.Core.Repositories;

namespace Switchback.Functions.Api;

public class ActivityApiFunctions
{
    private const int DefaultActivityCount = 50;
    private readonly IActivityRepository _activity;
    private readonly ILogger _logger;

    public ActivityApiFunctions(IActivityRepository activity, ILoggerFactory loggerFactory)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _logger = loggerFactory.CreateLogger<ActivityApiFunctions>();
    }

    [Function("ActivityList")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/activity")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        var list = await _activity.GetRecentAsync(userId, DefaultActivityCount);
        var dto = list.Select(a => new ActivityDto(a)).ToList();
        var json = JsonSerializer.Serialize(dto);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(json);
        return response;
    }
}

internal sealed class ActivityDto
{
    public DateTimeOffset ProcessedAt { get; }
    public string Subject { get; }
    public string RuleApplied { get; }
    public string Destination { get; }
    public string Provider { get; }
    public string MessageId { get; }

    public ActivityDto(Core.Entities.ActivityEntity e)
    {
        ProcessedAt = e.ProcessedAt;
        Subject = e.Subject;
        RuleApplied = e.RuleApplied;
        Destination = e.Destination;
        Provider = e.Provider;
        MessageId = e.MessageId;
    }
}
