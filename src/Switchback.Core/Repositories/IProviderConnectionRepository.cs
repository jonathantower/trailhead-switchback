using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IProviderConnectionRepository
{
    Task<ProviderConnectionEntity?> GetAsync(string userId, string provider, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderConnectionEntity>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(ProviderConnectionEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string provider, CancellationToken cancellationToken = default);
}
