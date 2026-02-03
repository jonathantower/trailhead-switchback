using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IGmailWatchRepository
{
    /// <summary>Gets the Gmail watch record for the user, if any.</summary>
    Task<GmailWatchEntity?> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Lists all Gmail watch records (for renewal timer).</summary>
    Task<IReadOnlyList<GmailWatchEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts the watch record (userId, expiration ms).</summary>
    Task UpsertAsync(GmailWatchEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes the watch record when user disconnects Gmail.</summary>
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
