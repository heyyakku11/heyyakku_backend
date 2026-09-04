using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Votes;
using Yakku.Application.Votes.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class VoteRepository : IVoteRepository
    {
        private readonly YakkuDbContext _context;

        public VoteRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Vote vote, CancellationToken cancellationToken = default)
        {
            await _context.Votes.AddAsync(vote, cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            Guid guestId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Votes.AnyAsync(
                vote => vote.GuestId == guestId && vote.PollId == pollId,
                cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Votes_GuestId_PollId"))
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
