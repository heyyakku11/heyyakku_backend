using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public interface IUserSettingsService
    {
        Task<UserSettingsResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Applies the settings that are present in the request and returns all of them.</summary>
        Task<UserSettingsResponse> UpdateAsync(
            Guid userId,
            UpdateUserSettingsRequest request,
            CancellationToken cancellationToken = default);
    }
}
