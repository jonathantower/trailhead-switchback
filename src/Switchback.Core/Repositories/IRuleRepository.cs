using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IRuleRepository
{
    Task<RuleEntity?> GetAsync(string userId, string ruleId, CancellationToken cancellationToken = default);
    /// <summary>Returns rules for the user sorted by Order ascending (priority order).</summary>
    Task<IReadOnlyList<RuleEntity>> GetOrderedRulesAsync(string userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(RuleEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string ruleId, CancellationToken cancellationToken = default);
}
