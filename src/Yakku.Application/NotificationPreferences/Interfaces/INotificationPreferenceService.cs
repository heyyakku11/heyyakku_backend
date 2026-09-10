using Yakku.Application.NotificationPreferences.DTOs;

namespace Yakku.Application.NotificationPreferences.Interfaces
{
    public interface INotificationPreferenceService
    {
        Task<NotificationPreferenceResponse> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<NotificationPreferenceResponse> UpdateAsync(
            Guid userId,
            UpdateNotificationPreferenceRequest request,
            CancellationToken cancellationToken = default);
    }
}
