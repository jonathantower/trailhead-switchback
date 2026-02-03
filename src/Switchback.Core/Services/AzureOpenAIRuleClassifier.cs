using System.Text;
using System.Text.Json;
namespace Switchback.Core.Services;

/// <summary>
/// Calls Azure OpenAI chat completions to classify email against rules. Returns exactly one rule name or null (NONE).
/// Config: AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, AzureOpenAI:Deployment (or Model), Pipeline:BodyTruncationChars (default 1000).
/// </summary>
public sealed class AzureOpenAIRuleClassifier : IRuleClassifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deployment;
    private const string ApiVersion = "2024-02-15-preview";

    public AzureOpenAIRuleClassifier(
        IHttpClientFactory httpClientFactory,
        string endpoint,
        string apiKey,
        string deployment)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _endpoint = (endpoint ?? "").TrimEnd('/');
        _apiKey = apiKey ?? "";
        _deployment = deployment ?? "";
    }

    public async Task<string?> ClassifyAsync(
        string from,
        string subject,
        string bodySnippet,
        IReadOnlyList<(string Name, string Prompt)> rules,
        CancellationToken cancellationToken = default)
    {
        if (rules == null || rules.Count == 0)
            return null;

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(from, subject, bodySnippet ?? "", rules);

        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = 100,
            temperature = 0d
        };

        var client = _httpClientFactory.CreateClient();
        var url = $"{_endpoint}/openai/deployments/{Uri.EscapeDataString(_deployment)}/chat/completions?api-version={ApiVersion}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseResponse(json, rules);
    }

    private static string BuildSystemPrompt()
    {
        return """
You are a classifier. You will be given an email (from, subject, body snippet) and a list of rules. Each rule has a name and a prompt describing when it applies.
You must respond with exactly ONE of:
- The rule name (exactly as given) if that rule applies to the email, OR
- The word NONE if no rule applies.

Respond with nothing else: only the rule name or NONE. No explanation, no punctuation, no extra text.
""";
    }

    private static string BuildUserPrompt(string from, string subject, string bodySnippet, IReadOnlyList<(string Name, string Prompt)> rules)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Email");
        sb.AppendLine($"From: {from}");
        sb.AppendLine($"Subject: {subject}");
        sb.AppendLine("Body (snippet):");
        sb.AppendLine(bodySnippet);
        sb.AppendLine();
        sb.AppendLine("## Rules (respond with exactly one rule name or NONE)");
        foreach (var (name, prompt) in rules)
            sb.AppendLine($"- {name}: {prompt}");
        sb.AppendLine();
        sb.AppendLine("Your response (one rule name or NONE):");
        return sb.ToString();
    }

    /// <summary>Parses chat completion response for a single rule name or NONE. Returns null for NONE or invalid response. Public for unit tests.</summary>
    public static string? ParseResponse(string json, IReadOnlyList<(string Name, string Prompt)> rules)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?
                .Trim();
            if (string.IsNullOrWhiteSpace(content)) return null;

            var normalized = content.Trim().ToUpperInvariant();
            if (normalized == "NONE") return null;

            var ruleNames = rules.Select(r => r.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ruleNames.Contains(content.Trim())) return content.Trim();

            var firstLine = content.Split('\n', '\r')[0].Trim();
            if (ruleNames.Contains(firstLine)) return firstLine;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
