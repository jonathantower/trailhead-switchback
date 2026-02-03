using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

namespace Switchback.Functions.Services;

/// <summary>
/// Processes one email: idempotency check, fetch, classify, apply rule (label/move), write activity, mark processed.
/// Config: Pipeline:BodyTruncationChars (default 1000), Pipeline:ActivityCap (default 50).
/// </summary>
public sealed class EmailPipelineService
{
    private const int DefaultBodyTruncationChars = 1000;
    private const int DefaultActivityCap = 50;

    private readonly IProcessedMessageRepository _processed;
    private readonly IAccessTokenProvider _tokens;
    private readonly IGmailMessageService _gmail;
    private readonly IM365MessageService _m365;
    private readonly IRuleRepository _rules;
    private readonly IRuleClassifier _classifier;
    private readonly IActivityRepository _activity;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailPipelineService> _logger;

    public EmailPipelineService(
        IProcessedMessageRepository processed,
        IAccessTokenProvider tokens,
        IGmailMessageService gmail,
        IM365MessageService m365,
        IRuleRepository rules,
        IRuleClassifier classifier,
        IActivityRepository activity,
        IConfiguration config,
        ILoggerFactory loggerFactory)
    {
        _processed = processed ?? throw new ArgumentNullException(nameof(processed));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _gmail = gmail ?? throw new ArgumentNullException(nameof(gmail));
        _m365 = m365 ?? throw new ArgumentNullException(nameof(m365));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = loggerFactory?.CreateLogger<EmailPipelineService>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Processes the message for the user and provider. No-op if already processed. On failure (fetch, classify, apply), email is left untouched.
    /// </summary>
    public async Task ProcessMessageAsync(string userId, string provider, string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(messageId))
        {
            _logger.LogWarning("ProcessMessage skipped: missing userId, provider, or messageId");
            return;
        }

        if (await _processed.ExistsAsync(provider, messageId, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug("Message {MessageId} already processed", messageId);
            return;
        }

        var accessToken = await _tokens.GetAccessTokenAsync(userId, provider, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("No access token for user {UserId} provider {Provider}", userId, provider);
            return;
        }

        string from, subject, bodySnippet;
        if (provider == ProviderConnectionEntity.ProviderGmail)
        {
            var fetched = await _gmail.FetchMessageAsync(accessToken, messageId, cancellationToken).ConfigureAwait(false);
            if (fetched == null)
            {
                _logger.LogWarning("Could not fetch Gmail message {MessageId}", messageId);
                return;
            }
            (from, subject, bodySnippet) = fetched.Value;
        }
        else if (provider == ProviderConnectionEntity.ProviderM365)
        {
            var fetched = await _m365.FetchMessageAsync(accessToken, messageId, cancellationToken).ConfigureAwait(false);
            if (fetched == null)
            {
                _logger.LogWarning("Could not fetch M365 message {MessageId}", messageId);
                return;
            }
            (from, subject, bodySnippet) = fetched.Value;
        }
        else
        {
            _logger.LogWarning("Unknown provider {Provider}", provider);
            return;
        }

        var truncation = _config.GetValue("Pipeline:BodyTruncationChars", DefaultBodyTruncationChars);
        if (truncation > 0 && bodySnippet.Length > truncation)
            bodySnippet = bodySnippet.Substring(0, truncation);

        var orderedRules = await _rules.GetOrderedRulesAsync(userId, cancellationToken).ConfigureAwait(false);
        var enabledRules = orderedRules.Where(r => r.Enabled).Select(r => (r.Name, r.Prompt)).ToList();
        if (enabledRules.Count == 0)
        {
            await WriteActivityAndMarkProcessedAsync(userId, provider, messageId, subject, "NONE", "", cancellationToken).ConfigureAwait(false);
            return;
        }

        var matchedRuleName = await _classifier.ClassifyAsync(from, subject, bodySnippet, enabledRules, cancellationToken).ConfigureAwait(false);
        var matchedRule = orderedRules.FirstOrDefault(r => r.Enabled && string.Equals(r.Name, matchedRuleName, StringComparison.OrdinalIgnoreCase));
        var ruleApplied = matchedRuleName ?? "NONE";
        var destination = matchedRule?.Destination ?? "";

        if (matchedRule != null)
        {
            var applied = provider == ProviderConnectionEntity.ProviderGmail
                ? await _gmail.ApplyLabelAsync(accessToken, messageId, matchedRule.Destination, cancellationToken).ConfigureAwait(false)
                : await _m365.MoveToFolderAsync(accessToken, messageId, matchedRule.Destination, cancellationToken).ConfigureAwait(false);
            if (!applied)
            {
                _logger.LogWarning("Could not apply rule {Rule} to message {MessageId}", matchedRule.Name, messageId);
                return;
            }
        }

        await WriteActivityAndMarkProcessedAsync(userId, provider, messageId, subject, ruleApplied, destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteActivityAndMarkProcessedAsync(string userId, string provider, string messageId, string subject, string ruleApplied, string destination, CancellationToken cancellationToken)
    {
        var processedAt = DateTimeOffset.UtcNow;
        var activityCap = _config.GetValue("Pipeline:ActivityCap", DefaultActivityCap);

        var activity = new ActivityEntity
        {
            PartitionKey = userId,
            RowKey = ActivityRowKeyHelper.ToRowKey(processedAt, messageId),
            ProcessedAt = processedAt,
            Subject = subject,
            RuleApplied = ruleApplied,
            Destination = destination,
            Provider = provider,
            MessageId = messageId
        };
        await _activity.AddWithCapAsync(activity, activityCap, cancellationToken).ConfigureAwait(false);
        await _processed.MarkProcessedAsync(provider, messageId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Processed message {MessageId}: rule={Rule}", messageId, ruleApplied);
    }
}
