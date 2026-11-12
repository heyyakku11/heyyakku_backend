using Yakku.Domain.Entities;

namespace Yakku.Application.AdminAuth.Interfaces
{
    /// <summary>
    /// Storage of admin sessions. The state changes below are single atomic statements (never read-modify-write),
    /// so two requests racing on the same session cannot both win a rotation or resurrect a revoked session.
    /// </summary>
    public interface IAdminSessionRepository
    {
        Task AddAsync(AdminSession session, CancellationToken cancellationToken = default);

        /// <summary>
        /// The session whose current token hash matches, or whose previous hash matches and is still inside its
        /// rotation grace. Includes the admin. Read-only.
        /// </summary>
        Task<AdminSession?> FindByHashAsync(string hash, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Swaps the token hash only if it is still <paramref name="expectedHash"/>. False when another request rotated first.</summary>
        Task<bool> TryRotateAsync(
            Guid sessionId,
            string expectedHash,
            string newHash,
            DateTime utcNow,
            DateTime graceUntil,
            DateTime idleExpiresAt,
            CancellationToken cancellationToken = default);

        /// <summary>Records activity and pushes the idle deadline forward on a session that is not revoked.</summary>
        Task TouchAsync(Guid sessionId, DateTime utcNow, DateTime idleExpiresAt, CancellationToken cancellationToken = default);

        /// <summary>Revokes one live session of that admin. False when it was already revoked, is another admin's, or does not exist.</summary>
        Task<bool> RevokeAsync(Guid adminId, Guid sessionId, string reason, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Revokes every live session of the admin and returns how many ended.</summary>
        Task<int> RevokeAllAsync(Guid adminId, string reason, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Deletes sessions that ended (revoked or past their absolute limit) before the cut-off.</summary>
        Task<int> DeleteEndedBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);
    }
}
