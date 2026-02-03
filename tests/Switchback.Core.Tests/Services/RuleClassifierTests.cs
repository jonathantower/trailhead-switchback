using Switchback.Core.Services;

namespace Switchback.Core.Tests.Services;

/// <summary>
/// Unit tests for AzureOpenAIRuleClassifier.ParseResponse (LLM output parsing) and prompt construction.
/// </summary>
public class RuleClassifierTests
{
    private static readonly IReadOnlyList<(string Name, string Prompt)> SampleRules = new[]
    {
        ("Work", "Emails related to work or office"),
        ("Personal", "Personal or family emails"),
        ("News", "Newsletters and updates")
    };

    [Test]
    public async Task ParseResponse_returns_null_for_NONE()
    {
        var json = """{"choices":[{"message":{"content":"NONE"}}]}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseResponse_returns_rule_name_when_exact_match()
    {
        var json = """{"choices":[{"message":{"content":"Work"}}]}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsEqualTo("Work");
    }

    [Test]
    public async Task ParseResponse_returns_rule_name_from_first_line()
    {
        var json = """{"choices":[{"message":{"content":"Personal\nSome extra text"}}]}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsEqualTo("Personal");
    }

    [Test]
    public async Task ParseResponse_returns_null_when_name_not_in_rules()
    {
        var json = """{"choices":[{"message":{"content":"Other"}}]}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseResponse_returns_null_for_empty_content()
    {
        var json = """{"choices":[{"message":{"content":""}}]}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseResponse_returns_null_for_invalid_json()
    {
        var result = AzureOpenAIRuleClassifier.ParseResponse("not json", SampleRules);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseResponse_returns_null_for_missing_choices()
    {
        var json = """{}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ParseResponse_is_case_insensitive_for_rule_name()
    {
        var json = """{"choices":[{"message":{"content":"work"}}]}""";
        var result = AzureOpenAIRuleClassifier.ParseResponse(json, SampleRules);
        await Assert.That(result).IsEqualTo("work");
    }
}
