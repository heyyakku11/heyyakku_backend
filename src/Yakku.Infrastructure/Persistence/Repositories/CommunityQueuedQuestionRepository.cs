using Microsoft.EntityFrameworkCore;
using Yakku.Application.Community.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class CommunityQueuedQuestionRepository : ICommunityQueuedQuestionRepository
    {
        private readonly YakkuDbContext _context;

        public CommunityQueuedQuestionRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            _context.CommunityQueuedQuestions
                .AsNoTracking()
                .CountAsync(question => question.UserId == userId, cancellationToken);

        public Task<List<CommunityQueuedQuestion>> ListByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            _context.CommunityQueuedQuestions
                .AsNoTracking()
                .Include(question => question.Tags)
                .Where(question => question.UserId == userId)
                .OrderBy(question => question.CreatedAt)
                .ThenBy(question => question.Id)
                .ToListAsync(cancellationToken);

        public Task<CommunityQueuedQuestion?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            _context.CommunityQueuedQuestions
                .Include(question => question.Tags)
                .FirstOrDefaultAsync(
                    question => question.Id == id && question.UserId == userId,
                    cancellationToken);

        public void Add(CommunityQueuedQuestion question) =>
            _context.CommunityQueuedQuestions.Add(question);

        public void Remove(CommunityQueuedQuestion question) =>
            _context.CommunityQueuedQuestions.Remove(question);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
