using Yakku.Domain.Entities;

namespace Yakku.Application.Email.Interfaces
{
    public interface IEmailLogRepository
    {
        Task AddAsync(EmailLog emailLog, CancellationToken cancellationToken = default);
        Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
