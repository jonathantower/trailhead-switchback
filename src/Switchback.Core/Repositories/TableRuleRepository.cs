using Azure.Data.Tables;
using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public sealed class TableRuleRepository : IRuleRepository
{
    private readonly TableClient _table;

    public TableRuleRepository(TableServiceClient tableServiceClient)
    {
        _table = tableServiceClient.GetTableClient(TableStorageConstants.TableRules);
    }

    public async Task<RuleEntity?> GetAsync(string userId, string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(ruleId);
        try
        {
            var response = await _table.GetEntityAsync<RuleEntity>(userId, ruleId, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<RuleEntity>> GetOrderedRulesAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        var list = new List<RuleEntity>();
        var safeUserId = TableStorageConstants.EscapeODataString(userId);
        await foreach (var entity in _table.QueryAsync<RuleEntity>(
            filter: $"PartitionKey eq '{safeUserId}'",
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            list.Add(entity);
        }
        return list.OrderBy(r => r.Order).ToList();
    }

    public async Task UpsertAsync(RuleEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string userId, string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(ruleId);
        await _table.DeleteEntityAsync(userId, ruleId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
