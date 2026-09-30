using Yakku.Domain.Entities;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminSessionRepository
    {
        Task AddAsync(AdminSession session, CancellationToken cancellationToken = default);
        Task<AdminSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task DeleteAsync(AdminSession session, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
