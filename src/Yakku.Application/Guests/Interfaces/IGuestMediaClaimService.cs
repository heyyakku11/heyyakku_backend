namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestMediaClaimService
    {
        /// <summary>
        /// Atomically transfers the guest's upload sessions to the user and deletes the guest, in one transaction.
        /// Blank, unknown, already-claimed and expired guests are left untouched. Any failure rolls everything back
        /// and is rethrown.
        /// </summary>
        Task<GuestClaimResult> ClaimAndDeleteGuestAsync(
            string? rawGuestToken,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Same as <see cref="ClaimAndDeleteGuestAsync"/> but never throws: a failure is audited and reported as
        /// <see cref="GuestClaimResult.Failed"/>. Use this wherever claiming is optional (login/registration).
        /// </summary>
        Task<GuestClaimResult> TryClaimAsync(
            string? rawGuestToken,
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
