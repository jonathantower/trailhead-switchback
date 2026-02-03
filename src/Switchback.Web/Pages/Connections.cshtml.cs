using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Switchback.Web.Pages;

[Authorize]
public class ConnectionsModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public ConnectionsModel(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public bool ConnectedGmail { get; set; }
    public bool ConnectedM365 { get; set; }
    public string ConnectGmailUrl { get; set; } = "";
    public string ConnectM365Url { get; set; } = "";
    public string DisconnectGmailUrl { get; set; } = "";
    public string DisconnectM365Url { get; set; } = "";
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return RedirectToPage("/Login");

        var functionsBaseUrl = _config["Functions:BaseUrl"]?.TrimEnd('/') ?? "";
        ConnectGmailUrl = $"{functionsBaseUrl}/api/auth/gmail/start?userId={Uri.EscapeDataString(userId)}";
        ConnectM365Url = $"{functionsBaseUrl}/api/auth/m365/start?userId={Uri.EscapeDataString(userId)}";
        DisconnectGmailUrl = $"{functionsBaseUrl}/api/auth/disconnect?userId={Uri.EscapeDataString(userId)}&provider=Gmail";
        DisconnectM365Url = $"{functionsBaseUrl}/api/auth/disconnect?userId={Uri.EscapeDataString(userId)}&provider=M365";

        if (!string.IsNullOrEmpty(functionsBaseUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{functionsBaseUrl}/api/connections?userId={Uri.EscapeDataString(userId)}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    ConnectedGmail = root.TryGetProperty("gmail", out var g) && g.GetBoolean();
                    ConnectedM365 = root.TryGetProperty("m365", out var m) && m.GetBoolean();
                }
            }
            catch
            {
                Message = "Could not load connection status. Check that the Functions API is reachable.";
            }
        }
        else
        {
            Message = "Functions:BaseUrl is not configured. Set it in appsettings to enable Connect/Disconnect.";
        }

        var connected = Request.Query["connected"].FirstOrDefault();
        var disconnected = Request.Query["disconnected"].FirstOrDefault();
        if (connected == "Gmail") Message = "Gmail connected successfully.";
        if (connected == "M365") Message = "Microsoft 365 connected successfully.";
        if (disconnected == "Gmail") Message = "Gmail disconnected.";
        if (disconnected == "M365") Message = "Microsoft 365 disconnected.";

        return Page();
    }
}
