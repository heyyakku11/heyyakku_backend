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
        Task DeleteAsync(UserSession session, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
