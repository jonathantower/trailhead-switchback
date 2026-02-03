using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;
using Switchback.Functions.Services;

namespace Switchback.Functions.Pipeline;

/// <summary>
/// HTTP trigger for Gmail Pub/Sub push. Receives base64 payload with emailAddress and historyId,
/// looks up userId, fetches history for new message IDs, and processes each via the pipeline.
/// </summary>
public class GmailPushFunctions
{
    private readonly IUserEmailRepository _userEmail;
    private readonly IAccessTokenProvider _tokens;
    private readonly IGmailMessageService _gmail;
    private readonly EmailPipelineService _pipeline;
    private readonly ILogger<GmailPushFunctions> _logger;

    public GmailPushFunctions(
        IUserEmailRepository userEmail,
        IAccessTokenProvider tokens,
        IGmailMessageService gmail,
        EmailPipelineService pipeline,
        ILoggerFactory loggerFactory)
    {
        _userEmail = userEmail ?? throw new ArgumentNullException(nameof(userEmail));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _gmail = gmail ?? throw new ArgumentNullException(nameof(gmail));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = loggerFactory?.CreateLogger<GmailPushFunctions>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    [Function("GmailPush")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook/gmail")] HttpRequestData req)
    {
        string? emailAddress = null;
        string? historyId = null;

        try
        {
            var body = await req.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Empty body");
                return bad;
            }

            var envelope = JsonDocument.Parse(body);
            var root = envelope.RootElement;
            if (!root.TryGetProperty("message", out var message))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing message");
                return bad;
            }
            if (!message.TryGetProperty("data", out var dataEl))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing message.data");
                return bad;
            }

            var dataB64 = dataEl.GetString();
            if (string.IsNullOrEmpty(dataB64))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Empty message.data");
                return bad;
            }

            var dataJson = Encoding.UTF8.GetString(Convert.FromBase64String(dataB64));
            var data = JsonDocument.Parse(dataJson).RootElement;
            emailAddress = data.TryGetProperty("emailAddress", out var ea) ? ea.GetString()?.Trim() : null;
            historyId = data.TryGetProperty("historyId", out var hi) ? hi.GetString()?.Trim() : null;

            if (string.IsNullOrEmpty(emailAddress) || string.IsNullOrEmpty(historyId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing emailAddress or historyId in payload");
                return bad;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid Gmail push payload");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid payload");
            return bad;
        }

        var userId = await _userEmail.GetUserIdAsync(ProviderConnectionEntity.ProviderGmail, emailAddress).ConfigureAwait(false);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No user found for Gmail address {Email}", emailAddress);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteStringAsync("OK");
            return ok;
        }

        var accessToken = await _tokens.GetAccessTokenAsync(userId, ProviderConnectionEntity.ProviderGmail).ConfigureAwait(false);
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("No access token for user {UserId}", userId);
            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteStringAsync("OK");
            return ok;
        }

        var messageIds = await _gmail.GetMessageIdsFromHistoryAsync(accessToken, historyId).ConfigureAwait(false);
        foreach (var messageId in messageIds)
            await _pipeline.ProcessMessageAsync(userId, ProviderConnectionEntity.ProviderGmail, messageId).ConfigureAwait(false);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("OK");
        return response;
    }
}
