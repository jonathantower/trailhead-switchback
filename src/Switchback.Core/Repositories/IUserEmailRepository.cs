using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IUserEmailRepository
{
    /// <summary>Gets userId for the given provider and email (email is normalized to lowercase).</summary>
    Task<string?> GetUserIdAsync(string provider, string email, CancellationToken cancellationToken = default);
    /// <summary>Stores or updates the mapping. Email is normalized to lowercase for RowKey.</summary>
    Task UpsertAsync(UserEmailEntity entity, CancellationToken cancellationToken = default);
    /// <summary>Removes the mapping when user disconnects the provider.</summary>
    Task DeleteAsync(string provider, string email, CancellationToken cancellationToken = default);
}
