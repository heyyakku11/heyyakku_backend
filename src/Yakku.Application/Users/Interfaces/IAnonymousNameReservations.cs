namespace Yakku.Application.Users.Interfaces
{
    /// <summary>
    /// Short-lived holds on candidate anonymous names, so two requests are not handed the same candidate at once
    /// and an unclaimed name frees itself when the hold expires. The database unique index stays the final
    /// authority: a hold narrows the race, it never replaces that check.
    /// </summary>
    public interface IAnonymousNameReservations
    {
        /// <summary>Holds the name for <paramref name="holdFor"/>. False when someone else already holds it.</summary>
        Task<bool> TryReserveAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default);

        /// <summary>Extends a hold the caller already owns (for example when an OTP is resent).</summary>
        Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default);

        Task ReleaseAsync(string name, CancellationToken cancellationToken = default);
    }
}
