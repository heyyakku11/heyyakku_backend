using Yakku.Domain.Entities;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IUserSessionRepository
    {
        Task AddAsync(UserSession session, CancellationToken cancellationToken = default);
        Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSession>> ListLiveByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSession>> ListByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        /// <summary>True when a live (unrevoked, unexpired) session other than the excluded one is linked to the device.</summary>
        Task<bool> HasLiveSessionForDeviceAsync(
            Guid deviceId,
            DateTime utcNow,
            Guid? exceptSessionId,
            CancellationToken cancellationToken = default);
        /// <summary>Reads the database (never a tracked copy): is the session still live for this user?</summary>
        Task<bool> IsLiveAsync(
            Guid sessionId,
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
        /// <summary>Locks the user row until the ambient transaction ends, serializing per-user session changes.</summary>
        Task LockUserAsync(Guid userId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Atomically replaces the refresh token hash if (and only if) the session still holds
        /// <paramref name="expectedHash"/> and is live. Returns false when a concurrent refresh or logout won.
        /// </summary>
        Task<bool> TryRotateAsync(
            Guid sessionId,
            string expectedHash,
            string newHash,
            DateTime newExpiresAt,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
        /// <summary>Deletes sessions that ended (revoked or expired) before the cutoff. Returns the count.</summary>
        Task<int> DeleteEndedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
        Task DeleteAsync(UserSession session, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
