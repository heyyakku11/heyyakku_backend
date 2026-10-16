namespace Yakku.Application.Guests.Interfaces
{
    public sealed record GuestCleanupResult(
        int Examined,
        int SessionsInvalidated,
        int GuestsDeleted,
        int GuestsDeferred,
        int GuestsFailed);

    public interface IGuestCleanupService
    {
        /// <summary>
        /// Expires guests that are past the grace period: invalidates their sessions, purges the stored media,
        /// then deletes the guest. A guest whose media could not be purged yet is deferred to the next run.
        /// </summary>
        Task<GuestCleanupResult> CleanupAsync(CancellationToken cancellationToken = default);
    }
}
