using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Switchback.Web.Pages.Rules;

[Authorize]
public class EditModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public EditModel(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public string Id { get; set; } = "";

    [BindProperty]
    [Required, MinLength(1), MaxLength(500)]
    [Display(Name = "Rule name")]
    public string Name { get; set; } = "";

    [BindProperty]
    [Required, MinLength(1)]
    [Display(Name = "Prompt (how to classify)")]
    public string Prompt { get; set; } = "";

    [BindProperty]
    [Required, MinLength(1), MaxLength(500)]
    [Display(Name = "Destination")]
    public string Destination { get; set; } = "";

    [BindProperty]
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; }

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");
        if (string.IsNullOrEmpty(id)) return RedirectToPage("Index");

        var baseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(baseUrl))
        {
            Error = "Functions:BaseUrl is not configured.";
            return Page();
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{baseUrl}/api/rules/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}");
        if (!response.IsSuccessStatusCode)
            return RedirectToPage("Index", new { message = "Rule not found." });

        var json = await response.Content.ReadAsStringAsync();
        var rule = JsonSerializer.Deserialize<RuleItem>(json, RulesJson.Options);
        if (rule == null)
            return RedirectToPage("Index", new { message = "Invalid rule." });

        Id = rule.Id;
        Name = rule.Name;
        Prompt = rule.Prompt;
        Destination = rule.Destination;
        Enabled = rule.Enabled;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");
        if (string.IsNullOrEmpty(id)) return RedirectToPage("Index");
        Id = id;

        if (!ModelState.IsValid)
            return Page();

        var baseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(baseUrl))
        {
            Error = "Functions:BaseUrl is not configured.";
            return Page();
        }

        var payload = new { Name = Name.Trim(), Prompt = Prompt.Trim(), Destination = Destination.Trim(), Enabled };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var client = _httpClientFactory.CreateClient();
        var response = await client.PutAsync($"{baseUrl}/api/rules/{Uri.EscapeDataString(id)}?userId={Uri.EscapeDataString(userId)}", content);

        if (response.IsSuccessStatusCode)
            return RedirectToPage("Index", new { message = "Rule updated." });

        var body = await response.Content.ReadAsStringAsync();
        Error = response.StatusCode == System.Net.HttpStatusCode.Conflict ? "A rule with this name already exists." : (body ?? "Update failed.");
        return Page();
    }
}
