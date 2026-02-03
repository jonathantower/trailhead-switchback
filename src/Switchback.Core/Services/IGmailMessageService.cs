namespace Switchback.Core.Services;

/// <summary>
/// Fetches Gmail message content and applies labels.
/// </summary>
public interface IGmailMessageService
{
    /// <summary>Fetches from, subject, and body snippet (caller truncates).</summary>
    Task<(string From, string Subject, string BodySnippet)?> FetchMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken = default);

    /// <summary>Adds a label to the message. destination is label name; looks up id via labels.list.</summary>
    Task<bool> ApplyLabelAsync(string accessToken, string messageId, string labelName, CancellationToken cancellationToken = default);

    /// <summary>Gets message IDs from history since startHistoryId (from Pub/Sub push).</summary>
    Task<IReadOnlyList<string>> GetMessageIdsFromHistoryAsync(string accessToken, string startHistoryId, CancellationToken cancellationToken = default);
}
