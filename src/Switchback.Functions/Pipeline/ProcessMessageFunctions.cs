using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Functions.Services;

namespace Switchback.Functions.Pipeline;

/// <summary>
/// Manual/test endpoint to process a single message: POST /api/process with userId, provider, messageId (query or body).
/// Used for M365 webhook or manual testing when Gmail Pub/Sub is not set up.
/// </summary>
public class ProcessMessageFunctions
{
    private readonly EmailPipelineService _pipeline;
    private readonly ILogger<ProcessMessageFunctions> _logger;

    public ProcessMessageFunctions(EmailPipelineService pipeline, ILoggerFactory loggerFactory)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = loggerFactory?.CreateLogger<ProcessMessageFunctions>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    [Function("ProcessMessage")]
    public async Task<HttpResponseData> Process(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/process")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        var provider = req.Query["provider"];
        var messageId = req.Query["messageId"];

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(messageId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId, provider, or messageId (query params)");
            return bad;
        }

        if (provider != ProviderConnectionEntity.ProviderGmail && provider != ProviderConnectionEntity.ProviderM365)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Provider must be Gmail or M365");
            return bad;
        }

        await _pipeline.ProcessMessageAsync(userId, provider, messageId).ConfigureAwait(false);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteStringAsync("Accepted");
        return response;
    }
}
