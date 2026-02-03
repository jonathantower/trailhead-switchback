using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Switchback.Core.Services;

public sealed class M365MessageService : IM365MessageService
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0/me";
    private readonly IHttpClientFactory _httpClientFactory;

    public M365MessageService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<(string From, string Subject, string BodySnippet)?> FetchMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/messages/{Uri.EscapeDataString(messageId)}?$select=from,subject,body");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var from = "";
        if (root.TryGetProperty("from", out var fromEl) && fromEl.TryGetProperty("emailAddress", out var addr))
        {
            var name = addr.TryGetProperty("name", out var n) ? n.GetString() : null;
            var address = addr.TryGetProperty("address", out var a) ? a.GetString() : null;
            from = string.IsNullOrEmpty(name) ? (address ?? "") : $"{name} <{address}>";
        }
        var subject = root.TryGetProperty("subject", out var sub) ? sub.GetString() ?? "" : "";
        var bodyContent = "";
        if (root.TryGetProperty("body", out var bodyEl))
        {
            bodyContent = bodyEl.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            if (bodyEl.TryGetProperty("contentType", out var ct) && ct.GetString() == "html")
                bodyContent = StripHtml(bodyContent);
        }
        return (from, subject, bodyContent);
    }

    public async Task<bool> MoveToFolderAsync(string accessToken, string messageId, string folderDisplayName, CancellationToken cancellationToken = default)
    {
        var folderId = await GetFolderIdByNameAsync(accessToken, folderDisplayName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(folderId)) return false;

        var client = _httpClientFactory.CreateClient();
        var body = JsonSerializer.Serialize(new { destinationId = folderId });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/messages/{Uri.EscapeDataString(messageId)}/move") { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private async Task<string?> GetFolderIdByNameAsync(string accessToken, string displayName, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/mailFolders?$top=100");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        foreach (var folder in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            if (folder.TryGetProperty("displayName", out var name) && string.Equals(name.GetString(), displayName, StringComparison.OrdinalIgnoreCase))
            {
                if (folder.TryGetProperty("id", out var id))
                    return id.GetString();
                break;
            }
        }
        return null;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var sb = new StringBuilder();
        var inTag = false;
        foreach (var c in html)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag && !char.IsWhiteSpace(c) || (sb.Length > 0 && !inTag))
                sb.Append(char.IsWhiteSpace(c) && sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]) ? "" : (char.IsWhiteSpace(c) ? ' ' : c));
        }
        return sb.ToString().Trim();
    }
}
