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

        public async Task AddAsync(Poll poll, CancellationToken cancellationToken = default)
        {
            await _context.Polls.AddAsync(poll, cancellationToken);
        }

        public void Delete(Poll poll)
        {
            _context.Polls.Remove(poll);
        }

        public async Task<Poll?> GetByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
        {
            return await _context.Polls
                .Include(poll => poll.Options)
                .Include(poll => poll.Media)
                .FirstOrDefaultAsync(poll => poll.ShareCode == shareCode, cancellationToken);
        }

        public async Task<Poll?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Polls
                .Include(poll => poll.Options)
                .Include(poll => poll.Media)
                .FirstOrDefaultAsync(poll => poll.Id == id, cancellationToken);
        }

        public async Task<Poll?> GetByIdAndCreatorAsync(
            Guid id,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Polls
                .Include(poll => poll.Options)
                .Include(poll => poll.Media)
                .FirstOrDefaultAsync(
                    poll => poll.Id == id && poll.CreatorId == creatorId,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<Poll>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Polls
                .AsNoTracking()
                .Include(poll => poll.Options)
                .Where(poll => poll.CreatorId == userId);

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

        public async Task<IReadOnlyList<Poll>> GetVisiblePollsAsync(
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            PollContentType? contentType,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Polls
                .AsNoTracking()
                .Include(poll => poll.Options)
                .Include(poll => poll.Media)
                .Where(poll =>
                    poll.Status == PollStatus.Active
                    && poll.Visibility == PollVisibility.Public);

            if (contentType is PollContentType selectedType)
            {
                query = query.Where(poll => poll.ContentType == selectedType);
            }

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

        public async Task<IReadOnlyList<Poll>> ListByStatusAsync(
            PollStatus? status,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Polls.AsNoTracking().AsQueryable();
            if (status is PollStatus selected)
            {
                query = query.Where(poll => poll.Status == selected);
            }

            return await query
                .OrderByDescending(poll => poll.CreatedAt)
                .ThenByDescending(poll => poll.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AnsweredPollEntry>> GetAnsweredByUserAsync(
            Guid userId,
            DateTime? cursorVotedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var voteQuery = _context.PollVotes
                .AsNoTracking()
                .Where(vote => vote.VoterId == userId);

            if (cursorVotedAt is not null && cursorPollId is not null)
            {
                voteQuery = voteQuery.Where(vote =>
                    vote.CreatedAt < cursorVotedAt.Value
                    || (vote.CreatedAt == cursorVotedAt.Value && vote.PollId < cursorPollId.Value));
            }

            var votes = await voteQuery
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.PollId)
                .Select(vote => new { vote.PollId, VotedAt = vote.CreatedAt })
                .Take(take)
                .ToListAsync(cancellationToken);

            if (votes.Count == 0)
            {
                return [];
            }

            var pollIds = votes.Select(vote => vote.PollId).ToArray();
            var polls = await _context.Polls
                .AsNoTracking()
                .Include(poll => poll.Options)
                .Where(poll => pollIds.Contains(poll.Id))
                .ToDictionaryAsync(poll => poll.Id, cancellationToken);

            return votes
                .Where(vote => polls.ContainsKey(vote.PollId))
                .Select(vote => new AnsweredPollEntry
                {
                    Poll = polls[vote.PollId],
                    VotedAt = vote.VotedAt
                })
                .ToList();
        }

        public async Task<IReadOnlyDictionary<Guid, PollVoteCountSnapshot>> GetVoteCountsAsync(
            IReadOnlyCollection<Guid> pollIds,
            CancellationToken cancellationToken = default)
        {
            if (pollIds.Count == 0)
            {
                return new Dictionary<Guid, PollVoteCountSnapshot>();
            }

            var rows = await _context.PollVotes
                .AsNoTracking()
                .Where(vote => pollIds.Contains(vote.PollId))
                .Join(
                    _context.Polls,
                    vote => vote.PollId,
                    poll => poll.Id,
                    (vote, poll) => new
                    {
                        vote.PollId,
                        vote.PollOptionId,
                        HasReason = vote.Reason != null,
                        IsOther = vote.VoterId != poll.CreatorId
                    })
                .GroupBy(item => new { item.PollId, item.PollOptionId })
                .Select(group => new
                {
                    group.Key.PollId,
                    group.Key.PollOptionId,
                    Count = group.Count(),
                    WhyCount = group.Count(item => item.HasReason),
                    OtherCount = group.Count(item => item.IsOther)
                })
                .ToListAsync(cancellationToken);

            return PollVoteCountSnapshot.Combine(rows.Select(row =>
                new PollVoteCountRow(row.PollId, row.PollOptionId, row.Count, row.WhyCount, row.OtherCount)));
        }

        public async Task<int> TryMarkActiveAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            return await _context.Polls
                .Where(poll =>
                    poll.Id == pollId
                    && poll.CreatorId == creatorId
                    && poll.Status == PollStatus.Draft)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(poll => poll.Status, PollStatus.Active)
                        .SetProperty(poll => poll.UpdatedAt, utcNow),
                    cancellationToken);
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
