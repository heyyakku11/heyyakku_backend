using Yakku.Domain.Entities;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IUserSessionRepository
    {
        Task AddAsync(UserSession session, CancellationToken cancellationToken = default);
        Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task DeleteAsync(UserSession session, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
