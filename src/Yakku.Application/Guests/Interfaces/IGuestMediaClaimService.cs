namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestMediaClaimService
    {
        /// <summary>
        /// Transfers claimable guest upload sessions to the user, then deletes the guest.
        /// No-op when <paramref name="rawGuestToken"/> is null/blank or unknown.
        /// </summary>
        Task ClaimAndDeleteGuestAsync(
            string? rawGuestToken,
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
