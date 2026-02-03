namespace Switchback.Core.Services;

/// <summary>
/// Classifier that always returns NONE. Used when Azure OpenAI is not configured.
/// </summary>
public sealed class NoOpRuleClassifier : IRuleClassifier
{
    public Task<string?> ClassifyAsync(string from, string subject, string bodySnippet, IReadOnlyList<(string Name, string Prompt)> rules, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
