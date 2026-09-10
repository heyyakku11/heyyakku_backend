using FluentValidation;
using Yakku.Application.NotificationPreferences.DTOs;
using Yakku.Application.NotificationPreferences.Interfaces;
using Yakku.Application.NotificationPreferences.Mapper;
using Yakku.Domain.Entities;

namespace Yakku.Application.NotificationPreferences.Services
{
    public class NotificationPreferenceService : INotificationPreferenceService
    {
        private readonly INotificationPreferenceRepository _preferences;
        private readonly IValidator<UpdateNotificationPreferenceRequest> _updateValidator;

        public NotificationPreferenceService(
            INotificationPreferenceRepository preferences,
            IValidator<UpdateNotificationPreferenceRequest> updateValidator)
        {
            _preferences = preferences;
            _updateValidator = updateValidator;
        }

        public async Task<NotificationPreferenceResponse> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var preference = await GetOrCreateAsync(userId, cancellationToken);
            return preference.ToResponse();
        }

        public async Task<NotificationPreferenceResponse> UpdateAsync(
            Guid userId,
            UpdateNotificationPreferenceRequest request,
            CancellationToken cancellationToken = default)
        {
            await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

            var preference = await GetOrCreateAsync(userId, cancellationToken);
            preference.Update(
                request.PushEnabled,
                request.PollActivityEnabled,
                request.OffersEnabled,
                request.AlertsEnabled,
                request.NormalEnabled);

            await _preferences.SaveChangesAsync(cancellationToken);
            return preference.ToResponse();
        }

        private async Task<NotificationPreference> GetOrCreateAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var existing = await _preferences.GetByUserIdAsync(userId, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var preference = new NotificationPreference(userId);
            await _preferences.AddAsync(preference, cancellationToken);
            await _preferences.SaveChangesAsync(cancellationToken);
            return preference;
        }
    }
}
