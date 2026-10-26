namespace Yakku.Application.Auth.Interfaces
{
    public sealed record SessionCleanupResult(int DevicesDetached, int SessionsDeleted);

    public interface ISessionCleanupService
    {
        /// <summary>
        /// Releases installations whose current session ended without a logout (expired, evicted elsewhere), then
        /// deletes sessions that ended long ago. Safe to run repeatedly and concurrently with logins.
        /// </summary>
        Task<SessionCleanupResult> CleanupAsync(CancellationToken cancellationToken = default);
    }
}
