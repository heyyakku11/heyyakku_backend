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

            // Personality votes: a guest vote on a card the user already voted on is dropped (one vote per user per
            // card), and that card's counter shrinks with it. The rest become the user's votes. The vote's trait rows
            // stay attached, and the "exactly one voter" check holds because the guest id is cleared in the same update.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE personality_cards c
                SET total_votes = c.total_votes - d.cnt
                FROM (
                    SELECT g.personality_card_id, count(*)::int AS cnt
                    FROM personality_votes g
                    WHERE g.voter_guest_id = {guestId}
                      AND EXISTS (
                          SELECT 1 FROM personality_votes u
                          WHERE u.personality_card_id = g.personality_card_id AND u.voter_user_id = {userId})
                    GROUP BY g.personality_card_id
                ) d
                WHERE c.id = d.personality_card_id
                """,
                cancellationToken);

            var personalityDropped = await _context.PersonalityVotes
                .Where(vote => vote.VoterGuestId == guestId
                               && _context.PersonalityVotes.Any(own =>
                                   own.PersonalityCardId == vote.PersonalityCardId && own.VoterUserId == userId))
                .ExecuteDeleteAsync(cancellationToken);

            var personalityTransferred = await _context.PersonalityVotes
                .Where(vote => vote.VoterGuestId == guestId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(vote => vote.VoterUserId, userId)
                        .SetProperty(vote => vote.VoterGuestId, (Guid?)null),
                    cancellationToken);

            return new GuestVoteClaimResult(transferred, dropped, personalityTransferred, personalityDropped);
        }
    }
}
