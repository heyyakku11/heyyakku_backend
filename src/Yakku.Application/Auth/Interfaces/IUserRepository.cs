using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<User>> ListAsync(UserStatus? status, CancellationToken cancellationToken = default);
        Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken cancellationToken = default);
        Task AddAsync(User user, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
