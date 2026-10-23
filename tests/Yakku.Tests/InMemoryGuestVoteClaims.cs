using Yakku.Application.Guests.Interfaces;

namespace Yakku.Tests;

/// <summary>Stands in for the vote re-attribution when a test does not care about votes; records the calls.</summary>
internal sealed class InMemoryGuestVoteClaims : IGuestVoteClaimRepository
{
    public List<(Guid GuestId, Guid UserId)> Calls { get; } = [];

    public GuestVoteClaimResult Result { get; set; } = new(0, 0);

    public Task<GuestVoteClaimResult> ClaimAsync(
        Guid guestId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((guestId, userId));
        return Task.FromResult(Result);
    }
}
