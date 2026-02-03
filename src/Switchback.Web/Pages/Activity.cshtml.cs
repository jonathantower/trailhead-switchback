using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Switchback.Web.Pages;

[Authorize]
public class ActivityModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public ActivityModel(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public List<ActivityItem> Activities { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");

        var baseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(baseUrl))
        {
            Error = "Functions:BaseUrl is not configured.";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{baseUrl}/api/activity?userId={Uri.EscapeDataString(userId)}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Activities = JsonSerializer.Deserialize<List<ActivityItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ActivityItem>();
            }
            else
            {
                Error = "Could not load activity.";
            }
        }
        catch
        {
            Error = "Could not reach the API. Check that the Functions app is running.";
        }

        return Page();
    }
}

public class ActivityItem
{
    public DateTimeOffset ProcessedAt { get; set; }
    public string Subject { get; set; } = "";
    public string RuleApplied { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Provider { get; set; } = "";
    public string MessageId { get; set; } = "";
}
