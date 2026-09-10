using Yakku.Application.NotificationPreferences.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.NotificationPreferences.Mapper
{
    internal static class NotificationPreferenceMappings
    {
        public static NotificationPreferenceResponse ToResponse(this NotificationPreference preference)
        {
            return new NotificationPreferenceResponse
            {
                PushEnabled = preference.PushEnabled,
                PollActivityEnabled = preference.PollActivityEnabled,
                OffersEnabled = preference.OffersEnabled,
                AlertsEnabled = preference.AlertsEnabled,
                NormalEnabled = preference.NormalEnabled
            };
        }
    }
}
