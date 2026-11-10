using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public interface IUserSettingsRepository
    {
        /// <summary>The user's settings, or null when they have no profile.</summary>
        Task<UserSettingsResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>One atomic UPDATE of the existing profile row. False when the user has no profile.</summary>
        Task<bool> SetAllowAskBackAsync(Guid userId, bool allow, CancellationToken cancellationToken = default);
    }
}
