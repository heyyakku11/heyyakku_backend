using Microsoft.EntityFrameworkCore;
using Yakku.Application.Email.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class EmailLogRepository : IEmailLogRepository
    {
        private readonly YakkuDbContext _context;

        public EmailLogRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task AddAsync(EmailLog emailLog, CancellationToken cancellationToken = default)
        {
            _context.EmailLogs.Add(emailLog);
            return Task.CompletedTask;
        }

        public Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.EmailLogs.FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
