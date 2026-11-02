using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminPolls.DTOs;
using Yakku.Application.AdminPolls.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AdminPollRepository : IAdminPollRepository
    {
        private readonly YakkuDbContext _context;

        public AdminPollRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<AdminPollPage> GetPollsAsync(
            AdminPollStatusFilter? status, DateTime utcNow, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Polls.AsNoTracking();

            // Polls never get the Expired status written; an active poll past its expiry counts as expired.
            query = status switch
            {
                AdminPollStatusFilter.Draft => query.Where(poll => poll.Status == PollStatus.Draft),
                AdminPollStatusFilter.Closed => query.Where(poll => poll.Status == PollStatus.Closed),
                AdminPollStatusFilter.Active => query.Where(poll =>
                    poll.Status == PollStatus.Active && (poll.ExpiresAt == null || poll.ExpiresAt > utcNow)),
                AdminPollStatusFilter.Expired => query.Where(poll =>
                    poll.Status == PollStatus.Expired
                    || (poll.Status == PollStatus.Active && poll.ExpiresAt != null && poll.ExpiresAt <= utcNow)),
                _ => query
            };

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(poll => poll.CreatedAt)
                .ThenBy(poll => poll.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(poll => new
                {
                    poll.Id,
                    poll.Question,
                    poll.ContentType,
                    poll.CreatedAt,
                    poll.ExpiresAt,
                    poll.Status,
                    CreatorAnonymousName = poll.Creator.Profile.AnonymousName,
                    VoteCount = _context.PollVotes.Count(vote => vote.PollId == poll.Id),
                    ReasonCount = _context.PollVotes.Count(vote => vote.PollId == poll.Id && vote.Reason != null)
                })
                .ToListAsync(cancellationToken);

            return new AdminPollPage
            {
                TotalCount = total,
                Items = rows.Select(row => new AdminPollResponse
                {
                    Id = row.Id,
                    Question = row.Question,
                    PollType = row.ContentType.ToString().ToLowerInvariant(),
                    CreatedAt = row.CreatedAt,
                    ExpiresAt = row.ExpiresAt,
                    Status = EffectiveStatus(row.Status, row.ExpiresAt, utcNow),
                    CreatorAnonymousName = row.CreatorAnonymousName,
                    VoteCount = row.VoteCount,
                    ReasonCount = row.ReasonCount
                }).ToList()
            };
        }

        private static string EffectiveStatus(PollStatus status, DateTime? expiresAt, DateTime utcNow)
        {
            if (status == PollStatus.Active && expiresAt is not null && expiresAt <= utcNow)
            {
                return "expired";
            }

            return status.ToString().ToLowerInvariant();
        }
    }
}
