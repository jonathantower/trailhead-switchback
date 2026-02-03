using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IActivityRepository
{
    /// <summary>Returns last N activities for the user, newest first. Supports capped storage (e.g. 50).</summary>
    Task<IReadOnlyList<ActivityEntity>> GetRecentAsync(string userId, int count, CancellationToken cancellationToken = default);
    /// <summary>Inserts activity and enforces cap: deletes oldest if count exceeds maxPerUser.</summary>
    Task AddWithCapAsync(ActivityEntity entity, int maxPerUser, CancellationToken cancellationToken = default);
}
