using Switchback.Core.Entities;

namespace Switchback.Core.Repositories;

public interface IUserRepository
{
    Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<UserEntity?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(UserEntity entity, CancellationToken cancellationToken = default);
}
