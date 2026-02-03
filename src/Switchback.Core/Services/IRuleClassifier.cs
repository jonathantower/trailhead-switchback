namespace Switchback.Core.Services;

/// <summary>
/// LLM-based classifier: given email context and ordered rules, returns exactly one rule name or null (NONE).
/// </summary>
public interface IRuleClassifier
{
    /// <summary>
    /// Classifies the email against the given rules. Returns the matching rule name, or null for NONE.
    /// </summary>
    /// <param name="from">Sender display name and email (e.g. "John &lt;john@example.com&gt;").</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="bodySnippet">First N characters of body (truncated).</param>
    /// <param name="rules">Ordered list of (Name, Prompt) for enabled rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rule name if one matches, or null for NONE. Any other response is treated as failure.</returns>
    Task<string?> ClassifyAsync(
        string from,
        string subject,
        string bodySnippet,
        IReadOnlyList<(string Name, string Prompt)> rules,
        CancellationToken cancellationToken = default);
}
