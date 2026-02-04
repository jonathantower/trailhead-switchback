using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Switchback.Core.Entities;
using Switchback.Core.Repositories;

namespace Switchback.Functions.Api;

internal static class RulesJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}

public class RulesApiFunctions
{
    private readonly IConfiguration _config;
    private readonly IRuleRepository _rules;
    private readonly ILogger _logger;

    public RulesApiFunctions(
        IConfiguration config,
        IRuleRepository rules,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _rules = rules;
        _logger = loggerFactory.CreateLogger<RulesApiFunctions>();
    }

    [Function("RulesList")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/rules")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        var list = await _rules.GetOrderedRulesAsync(userId);
        var dto = list.Select(r => new RuleDto(r)).ToList();
        var json = JsonSerializer.Serialize(dto);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(json);
        return response;
    }

    [Function("RulesGet")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/rules/{id}")] HttpRequestData req,
        string id)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(id))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId or id");
            return bad;
        }

        var rule = await _rules.GetAsync(userId, id);
        if (rule == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            return notFound;
        }

        var json = JsonSerializer.Serialize(new RuleDto(rule));
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(json);
        return response;
    }

    [Function("RulesCreate")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/rules")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        RuleCreateDto? dto;
        try
        {
            var body = await req.ReadAsStringAsync();
            dto = JsonSerializer.Deserialize<RuleCreateDto>(body ?? "{}", RulesJson.Options);
        }
        catch
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON");
            return bad;
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Prompt) || string.IsNullOrWhiteSpace(dto.Destination))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Name, Prompt, and Destination are required");
            return bad;
        }

        var existing = await _rules.GetOrderedRulesAsync(userId);
        if (existing.Any(r => string.Equals(r.Name, dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync("Name must be unique");
            return conflict;
        }

        var maxOrder = existing.Count == 0 ? -1 : existing.Max(r => r.Order);
        var entity = new RuleEntity
        {
            PartitionKey = userId,
            RowKey = Guid.NewGuid().ToString("N"),
            Name = dto.Name.Trim(),
            Prompt = dto.Prompt.Trim(),
            Destination = dto.Destination.Trim(),
            Enabled = dto.Enabled,
            Order = maxOrder + 1
        };
        await _rules.UpsertAsync(entity);

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new RuleDto(entity)));
        return response;
    }

    [Function("RulesUpdate")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "api/rules/{id}")] HttpRequestData req,
        string id)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(id))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId or id");
            return bad;
        }

        var rule = await _rules.GetAsync(userId, id);
        if (rule == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            return notFound;
        }

        RuleCreateDto? dto;
        try
        {
            var body = await req.ReadAsStringAsync();
            dto = JsonSerializer.Deserialize<RuleCreateDto>(body ?? "{}", RulesJson.Options);
        }
        catch
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON");
            return bad;
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Prompt) || string.IsNullOrWhiteSpace(dto.Destination))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Name, Prompt, and Destination are required");
            return bad;
        }

        var existing = await _rules.GetOrderedRulesAsync(userId);
        if (existing.Any(r => r.RowKey != id && string.Equals(r.Name, dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync("Name must be unique");
            return conflict;
        }

        rule.Name = dto.Name.Trim();
        rule.Prompt = dto.Prompt.Trim();
        rule.Destination = dto.Destination.Trim();
        rule.Enabled = dto.Enabled;
        await _rules.UpsertAsync(rule);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new RuleDto(rule)));
        return response;
    }

    [Function("RulesDelete")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "api/rules/{id}")] HttpRequestData req,
        string id)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(id))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId or id");
            return bad;
        }

        var rule = await _rules.GetAsync(userId, id);
        if (rule == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            return notFound;
        }

        await _rules.DeleteAsync(userId, id);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    [Function("RulesReorder")]
    public async Task<HttpResponseData> Reorder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/rules/reorder")] HttpRequestData req)
    {
        var userId = req.Query["userId"];
        if (string.IsNullOrEmpty(userId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing userId");
            return bad;
        }

        string[]? ids;
        try
        {
            var body = await req.ReadAsStringAsync();
            ids = JsonSerializer.Deserialize<string[]>(body ?? "[]");
        }
        catch
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON: expected array of rule IDs");
            return bad;
        }

        if (ids == null || ids.Length == 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Rule IDs array is required");
            return bad;
        }

        var existing = await _rules.GetOrderedRulesAsync(userId);
        var existingIds = existing.Select(r => r.RowKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ids.Any(id => !existingIds.Contains(id)))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("All IDs must belong to the user's rules");
            return bad;
        }

        for (var i = 0; i < ids.Length; i++)
        {
            var rule = existing.First(r => string.Equals(r.RowKey, ids[i], StringComparison.OrdinalIgnoreCase));
            if (rule.Order != i)
            {
                rule.Order = i;
                await _rules.UpsertAsync(rule);
            }
        }

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}

internal sealed class RuleDto
{
    public string Id { get; }
    public string Name { get; }
    public string Prompt { get; }
    public string Destination { get; }
    public bool Enabled { get; }
    public int Order { get; }

    public RuleDto(RuleEntity e)
    {
        Id = e.RowKey;
        Name = e.Name;
        Prompt = e.Prompt;
        Destination = e.Destination;
        Enabled = e.Enabled;
        Order = e.Order;
    }
}

internal sealed class RuleCreateDto
{
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
