using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Switchback.Web.Pages.Rules;

[Authorize]
public class AddModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public AddModel(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

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
    public bool Enabled { get; set; } = true;

    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");
        if (string.IsNullOrEmpty(_config["Functions:BaseUrl"]?.Trim()))
        {
            Error = "Functions:BaseUrl is not configured.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");

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
        var response = await client.PostAsync($"{baseUrl}/api/rules?userId={Uri.EscapeDataString(userId)}", content);

        if (response.IsSuccessStatusCode)
            return RedirectToPage("Index", new { message = "Rule created." });

        var body = await response.Content.ReadAsStringAsync();
        Error = response.StatusCode == System.Net.HttpStatusCode.Conflict ? "A rule with this name already exists." : (body ?? "Create failed.");
        return Page();
    }
}
