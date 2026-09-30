using Yakku.Domain.Entities;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminRepository
    {
        Task<Admin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Admin?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task AddAsync(Admin admin, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
