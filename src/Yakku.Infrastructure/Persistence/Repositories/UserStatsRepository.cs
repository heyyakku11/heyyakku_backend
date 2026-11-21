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
            // One round-trip: four scalar subqueries (DbContext is not safe for parallel queries).
            var row = await _context.Database
                .SqlQuery<ActivityCountRow>($"""
                    SELECT
                      (SELECT COUNT(*)::int FROM polls p
                       WHERE p.creator_id = {userId} AND p.status <> 'DRAFT') AS "CardsPolls",
                      (SELECT COUNT(*)::int FROM user_checks uc
                       WHERE uc.user_id = {userId}) AS "CardsChecks",
                      (SELECT COUNT(*)::int FROM poll_votes pv
                       WHERE pv.voter_id = {userId}
                         AND pv.voter_role = 'USER'
                         AND NOT EXISTS (
                           SELECT 1 FROM polls p WHERE p.id = pv.poll_id AND p.creator_id = {userId}
                         )) AS "VotesPolls",
                      (SELECT COUNT(*)::int FROM user_check_votes ucv
                       WHERE ucv.voter_id = {userId}
                         AND ucv.voter_role = 'USER'
                         AND NOT EXISTS (
                           SELECT 1 FROM user_checks uc
                           WHERE uc.id = ucv.user_check_id AND uc.user_id = {userId}
                         )) AS "VotesChecks"
                    """)
                .AsNoTracking()
                .SingleAsync(cancellationToken);

            return new UserActivityCounts(
                row.CardsPolls + row.CardsChecks,
                row.VotesPolls + row.VotesChecks);
        }

        private sealed class ActivityCountRow
        {
            public int CardsPolls { get; set; }
            public int CardsChecks { get; set; }
            public int VotesPolls { get; set; }
            public int VotesChecks { get; set; }
        }
    }
}
