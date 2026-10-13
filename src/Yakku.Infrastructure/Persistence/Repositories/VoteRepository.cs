using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Votes;
using Yakku.Application.Votes.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class VoteRepository : IVoteRepository
    {
        private const string OneVotePerPollConstraint = "IX_poll_votes_poll_id_voter_id";

        private readonly YakkuDbContext _context;

        public VoteRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(PollVote vote, CancellationToken cancellationToken = default)
        {
            await _context.PollVotes.AddAsync(vote, cancellationToken);
        }

        public async Task<bool> ExistsForUserAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            return await _context.PollVotes.AnyAsync(
                vote => vote.VoterId == userId && vote.PollId == pollId,
                cancellationToken);
        }

        public async Task<PollVote?> GetByUserAndPollAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            return await _context.PollVotes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    vote => vote.VoterId == userId && vote.PollId == pollId,
                    cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, OneVotePerPollConstraint))
            {
                throw VoteExceptions.AlreadyVoted();
            }
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres &&
                   postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                   string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
