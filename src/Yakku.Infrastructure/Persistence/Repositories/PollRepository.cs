using Microsoft.EntityFrameworkCore;
using Yakku.Application.Polls;
using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class PollRepository : IPollRepository
    {
        private readonly YakkuDbContext _context;

        public PollRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Polls poll, CancellationToken cancellationToken = default)
        {
            await _context.Polls.AddAsync(poll, cancellationToken);
        }

        public async Task<Polls?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Polls
                .Include(p => p.Options)
                .ThenInclude(o => o.Image)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<Polls?> GetByIdAndCreatorAsync(
            Guid id,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Polls
                .Include(p => p.Options)
                .ThenInclude(o => o.Image)
                .FirstOrDefaultAsync(
                    p => p.Id == id && p.CreatorId == creatorId,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Polls>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Polls
                .AsNoTracking()
                .Include(poll => poll.Options)
                .Where(poll => poll.CreatorId == userId && poll.Status != PollStatus.Deleted);

            if (cursorCreatedAt is not null && cursorId is not null)
            {
                query = query.Where(poll =>
                    poll.CreatedAt < cursorCreatedAt.Value
                    || (poll.CreatedAt == cursorCreatedAt.Value && poll.Id < cursorId.Value));
            }

            return await query
                .OrderByDescending(poll => poll.CreatedAt)
                .ThenByDescending(poll => poll.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AnsweredPollEntry>> GetAnsweredByUserAsync(
            Guid userId,
            DateTime? cursorVotedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query =
                from vote in _context.Votes.AsNoTracking()
                join poll in _context.Polls.AsNoTracking().Include(p => p.Options)
                    on vote.PollId equals poll.Id
                where vote.UserId == userId && poll.Status != PollStatus.Deleted
                select new { Poll = poll, VotedAt = vote.CreatedAt };

            if (cursorVotedAt is not null && cursorPollId is not null)
            {
                query = query.Where(row =>
                    row.VotedAt < cursorVotedAt.Value
                    || (row.VotedAt == cursorVotedAt.Value && row.Poll.Id < cursorPollId.Value));
            }

            var rows = await query
                .OrderByDescending(row => row.VotedAt)
                .ThenByDescending(row => row.Poll.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new AnsweredPollEntry
                {
                    Poll = row.Poll,
                    VotedAt = row.VotedAt
                })
                .ToList();
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
