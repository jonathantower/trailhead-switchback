using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;
using Switchback.Functions.Services;

namespace Switchback.Functions.Pipeline;

/// <summary>
/// Timer-triggered renewal of Gmail push watches. Runs daily; renews any watch expiring within the next 24 hours.
/// Config: Gmail:PubSubTopic (required for renewal).
/// </summary>
public class GmailWatchRenewalFunctions
{
    private const string GmailWatchUrl = "https://gmail.googleapis.com/gmail/v1/users/me/watch";
    private static readonly TimeSpan RenewBefore = TimeSpan.FromHours(24);

    private readonly IGmailWatchRepository _gmailWatch;
    private readonly IProviderConnectionRepository _connections;
    private readonly IAccessTokenProvider _tokens;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GmailWatchRenewalFunctions> _logger;

    public GmailWatchRenewalFunctions(
        IGmailWatchRepository gmailWatch,
        IProviderConnectionRepository connections,
        IAccessTokenProvider tokens,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _gmailWatch = gmailWatch ?? throw new ArgumentNullException(nameof(gmailWatch));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = loggerFactory?.CreateLogger<GmailWatchRenewalFunctions>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    [Function("GmailWatchRenewal")]
    public async Task Run(
        [TimerTrigger("0 0 2 * * *", RunOnStartup = false)] TimerInfo timerInfo)
    {
        var topic = _config["Gmail:PubSubTopic"]?.Trim();
        if (string.IsNullOrEmpty(topic))
        {
            _logger.LogDebug("Gmail:PubSubTopic not set; skipping renewal");
            return;
        }

        var all = await _gmailWatch.GetAllAsync();
        var thresholdMs = DateTimeOffset.UtcNow.Add(RenewBefore).ToUnixTimeMilliseconds();
        var toRenew = all.Where(w => w.ExpiresAtMs < thresholdMs).ToList();

        foreach (var watch in toRenew)
        {
            var userId = watch.RowKey;
            var accessToken = await _tokens.GetAccessTokenAsync(userId, ProviderConnectionEntity.ProviderGmail);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No token for user {UserId}; skipping watch renewal", userId);
                continue;
            }

            using var http = _httpClientFactory.CreateClient();
            var body = JsonSerializer.Serialize(new { topicName = topic });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, GmailWatchUrl) { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Watch renewal failed for user {UserId}: {Status}", userId, response.StatusCode);
                continue;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            long expirationMs = 0;
            if (doc.RootElement.TryGetProperty("expiration", out var expEl))
                expirationMs = expEl.ValueKind == JsonValueKind.Number ? expEl.GetInt64() : (long.TryParse(expEl.GetString(), out var ms) ? ms : 0);

            if (expirationMs > 0)
            {
                watch.ExpiresAtMs = expirationMs;
                await _gmailWatch.UpsertAsync(watch);

                var connection = await _connections.GetAsync(userId, ProviderConnectionEntity.ProviderGmail);
                if (connection != null)
                {
                    connection.GmailWatchExpiresAtMs = expirationMs;
                    await _connections.UpsertAsync(connection);
                }
                _logger.LogInformation("Renewed Gmail watch for user {UserId}", userId);
            }
        }
    }
}
