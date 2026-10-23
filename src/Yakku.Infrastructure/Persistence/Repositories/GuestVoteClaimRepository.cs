using Microsoft.EntityFrameworkCore;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class GuestVoteClaimRepository : IGuestVoteClaimRepository
    {
        private readonly YakkuDbContext _context;

        public GuestVoteClaimRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<GuestVoteClaimResult> ClaimAsync(
            Guid guestId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Set-based statements run in the caller's transaction (same DbContext), so they commit or roll back
            // together with the guest delete.
            var dropped = await _context.PollVotes
                .Where(vote => vote.VoterId == guestId
                               && _context.PollVotes.Any(own => own.PollId == vote.PollId && own.VoterId == userId))
                .ExecuteDeleteAsync(cancellationToken);

            dropped += await _context.UserCheckVotes
                .Where(vote => vote.VoterId == guestId
                               && _context.UserCheckVotes.Any(own => own.UserCheckId == vote.UserCheckId && own.VoterId == userId))
                .ExecuteDeleteAsync(cancellationToken);

            var transferred = await _context.PollVotes
                .Where(vote => vote.VoterId == guestId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(vote => vote.VoterId, userId)
                        .SetProperty(vote => vote.VoterRole, VoterRole.User),
                    cancellationToken);

            transferred += await _context.UserCheckVotes
                .Where(vote => vote.VoterId == guestId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(vote => vote.VoterId, userId)
                        .SetProperty(vote => vote.VoterRole, VoterRole.User),
                    cancellationToken);

            return new GuestVoteClaimResult(transferred, dropped);
        }
    }
}
