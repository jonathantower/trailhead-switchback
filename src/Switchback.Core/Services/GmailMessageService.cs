using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Switchback.Core.Services;

public sealed class GmailMessageService : IGmailMessageService
{
    private const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me";
    private readonly IHttpClientFactory _httpClientFactory;

    public GmailMessageService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<(string From, string Subject, string BodySnippet)?> FetchMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/messages/{Uri.EscapeDataString(messageId)}?format=full");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var from = GetHeader(root, "From");
        var subject = GetHeader(root, "Subject");
        var bodySnippet = GetBodySnippet(root);

        return (from, subject, bodySnippet);
    }

    public async Task<bool> ApplyLabelAsync(string accessToken, string messageId, string labelName, CancellationToken cancellationToken = default)
    {
        var labelId = await GetLabelIdByNameAsync(accessToken, labelName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(labelId)) return false;

        var client = _httpClientFactory.CreateClient();
        var body = JsonSerializer.Serialize(new { addLabelIds = new[] { labelId } });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/messages/{Uri.EscapeDataString(messageId)}/modify") { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<string>> GetMessageIdsFromHistoryAsync(string accessToken, string startHistoryId, CancellationToken cancellationToken = default)
    {
        var list = new List<string>();
        var client = _httpClientFactory.CreateClient();
        var url = $"{BaseUrl}/history?startHistoryId={Uri.EscapeDataString(startHistoryId)}&historyTypes=messageAdded";
        while (!string.IsNullOrEmpty(url))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return list;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("history", out var history))
            {
                foreach (var h in history.EnumerateArray())
                {
                    if (h.TryGetProperty("messagesAdded", out var added))
                    {
                        foreach (var ma in added.EnumerateArray())
                        {
                            if (ma.TryGetProperty("message", out var msg) && msg.TryGetProperty("id", out var id))
                            {
                                var idStr = id.GetString();
                                if (!string.IsNullOrEmpty(idStr)) list.Add(idStr);
                            }
                        }
                    }
                }
            }

            url = "";
            if (root.TryGetProperty("nextPageToken", out var next) && next.ValueKind == JsonValueKind.String)
            {
                var token = next.GetString();
                if (!string.IsNullOrEmpty(token))
                    url = $"{BaseUrl}/history?startHistoryId={Uri.EscapeDataString(startHistoryId)}&historyTypes=messageAdded&pageToken={Uri.EscapeDataString(token)}";
            }
        }
        return list;
    }

    private async Task<string?> GetLabelIdByNameAsync(string accessToken, string labelName, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/labels");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        foreach (var label in doc.RootElement.GetProperty("labels").EnumerateArray())
        {
            if (label.TryGetProperty("name", out var name) && string.Equals(name.GetString(), labelName, StringComparison.OrdinalIgnoreCase))
            {
                if (label.TryGetProperty("id", out var id))
                    return id.GetString();
                break;
            }
        }
        return null;
    }

    private static string GetHeader(JsonElement messageRoot, string headerName)
    {
        if (!messageRoot.TryGetProperty("payload", out var payload)) return "";
        if (!payload.TryGetProperty("headers", out var headers)) return "";
        foreach (var h in headers.EnumerateArray())
        {
            if (h.TryGetProperty("name", out var name) && string.Equals(name.GetString(), headerName, StringComparison.OrdinalIgnoreCase))
            {
                if (h.TryGetProperty("value", out var value))
                    return value.GetString() ?? "";
                break;
            }
        }
        return "";
    }

    private static string GetBodySnippet(JsonElement messageRoot)
    {
        if (messageRoot.TryGetProperty("snippet", out var snippet))
            return snippet.GetString() ?? "";

        if (!messageRoot.TryGetProperty("payload", out var payload)) return "";
        var partBody = GetPartBody(payload);
        if (!string.IsNullOrEmpty(partBody)) return partBody;
        if (payload.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                partBody = GetPartBody(part);
                if (!string.IsNullOrEmpty(partBody)) return partBody;
            }
        }
        return "";
    }

    private static string GetPartBody(JsonElement part)
    {
        if (!part.TryGetProperty("body", out var body)) return "";
        if (!body.TryGetProperty("data", out var data)) return "";
        var b64 = data.GetString();
        if (string.IsNullOrEmpty(b64)) return "";
        try
        {
            var bytes = Convert.FromBase64String(b64.Replace('-', '+').Replace('_', '/'));
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
