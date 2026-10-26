using Microsoft.EntityFrameworkCore;
using Yakku.Application.Users.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserStatsRepository : IUserStatsRepository
    {
        private readonly YakkuDbContext _context;

        public UserStatsRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<UserActivityCounts> GetActivityCountsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var polls = await _context.Polls
                .CountAsync(poll => poll.CreatorId == userId && poll.Status != PollStatus.Draft, cancellationToken);
            var checks = await _context.UserChecks
                .CountAsync(check => check.UserId == userId, cancellationToken);

            // voter_id holds a user id or a guest id, so the role is part of the match. The owner's own answer on a
            // poll or check they made is excluded.
            var pollVotes = await _context.PollVotes
                .CountAsync(vote => vote.VoterId == userId
                    && vote.VoterRole == VoterRole.User
                    && !_context.Polls.Any(poll => poll.Id == vote.PollId && poll.CreatorId == userId),
                    cancellationToken);
            var checkVotes = await _context.UserCheckVotes
                .CountAsync(vote => vote.VoterId == userId
                    && vote.VoterRole == VoterRole.User
                    && !_context.UserChecks.Any(check => check.Id == vote.UserCheckId && check.UserId == userId),
                    cancellationToken);

            return new UserActivityCounts(polls + checks, pollVotes + checkVotes);
        }
    }
}
