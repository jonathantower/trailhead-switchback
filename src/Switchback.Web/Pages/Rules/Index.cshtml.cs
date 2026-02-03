using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Switchback.Web.Pages.Rules;

internal static class RulesJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}

[Authorize]
public class IndexModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public List<RuleItem> Rules { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string FunctionsBaseUrl { get; set; } = "";
    public string UserId { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");
        UserId = userId;

        FunctionsBaseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(FunctionsBaseUrl))
        {
            Error = "Functions:BaseUrl is not configured.";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{FunctionsBaseUrl}/api/rules?userId={Uri.EscapeDataString(userId)}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<RuleItem>>(json, RulesJson.Options) ?? new List<RuleItem>();
                Rules = list;
            }
            else
            {
                Error = "Could not load rules.";
            }
        }
        catch
        {
            Error = "Could not reach the API. Check that the Functions app is running.";
        }

        Message = Request.Query["message"].FirstOrDefault();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(id)) return RedirectToPage();

        var baseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(baseUrl)) return RedirectToPage(new { error = "API not configured" });

        var client = _httpClientFactory.CreateClient();
        var response = await client.DeleteAsync($"{baseUrl}/api/rules/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}");
        return RedirectToPage(new { message = response.IsSuccessStatusCode ? "Rule deleted." : "Delete failed." });
    }

    public async Task<IActionResult> OnPostToggleAsync(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(id)) return RedirectToPage();

        var baseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(baseUrl)) return RedirectToPage(new { error = "API not configured" });

        var client = _httpClientFactory.CreateClient();
        var getResponse = await client.GetAsync($"{baseUrl}/api/rules/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}");
        if (!getResponse.IsSuccessStatusCode) return RedirectToPage(new { message = "Could not load rule." });

        var json = await getResponse.Content.ReadAsStringAsync();
        var rule = JsonSerializer.Deserialize<RuleItem>(json, RulesJson.Options);
        if (rule == null) return RedirectToPage(new { message = "Invalid rule." });

        var payload = new { rule.Name, rule.Prompt, rule.Destination, Enabled = !rule.Enabled };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var putResponse = await client.PutAsync($"{baseUrl}/api/rules/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}", content);
        return RedirectToPage(new { message = putResponse.IsSuccessStatusCode ? (rule.Enabled ? "Rule disabled." : "Rule enabled.") : "Update failed." });
    }

    public async Task<IActionResult> OnPostReorderAsync(string order)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(order)) return RedirectToPage();

        var baseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(baseUrl)) return RedirectToPage(new { error = "API not configured" });

        string[] ids;
        try
        {
            ids = order.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch
        {
            return RedirectToPage(new { message = "Invalid order." });
        }

        if (ids.Length == 0) return RedirectToPage();

        var client = _httpClientFactory.CreateClient();
        var body = new StringContent(JsonSerializer.Serialize(ids), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{baseUrl}/api/rules/reorder?userId={Uri.EscapeDataString(userId)}", body);
        return RedirectToPage(new { message = response.IsSuccessStatusCode ? "Order updated." : "Reorder failed." });
    }
}

public class RuleItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Enabled { get; set; }
    public int Order { get; set; }
}
