namespace Yakku.Application.Guests.Interfaces
{
    /// <param name="Transferred">Guest votes now owned by the user.</param>
    /// <param name="Dropped">Guest votes discarded because the user already voted on the same poll or check.</param>
    public sealed record GuestVoteClaimResult(int Transferred, int Dropped);

    public interface IGuestVoteClaimRepository
    {
        /// <summary>
        /// Moves a guest's poll and check votes to the user (role USER). A guest vote on something the user has
        /// already voted on is dropped, so the one-vote-per-voter rule still holds. Not atomic on its own: call it
        /// inside the guest repository transaction together with the guest delete.
        /// </summary>
        Task<GuestVoteClaimResult> ClaimAsync(
            Guid guestId,
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
