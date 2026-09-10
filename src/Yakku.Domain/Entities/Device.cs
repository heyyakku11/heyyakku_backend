using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Device
    {
        public Guid Id { get; private set; }
        public Guid? UserId { get; private set; }
        public string InstallationId { get; private set; } = string.Empty;
        public string? PushToken { get; private set; }
        public DevicePlatform Platform { get; private set; }
        public string? DeviceModel { get; private set; }
        public string? OsVersion { get; private set; }
        public string? AppVersion { get; private set; }
        public string? AppBuild { get; private set; }
        public string? Locale { get; private set; }
        public string? Timezone { get; private set; }
        public NotificationPermissionStatus? NotificationPermission { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime? LastSeenAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User? User { get; private set; }
        public ICollection<UserSession> Sessions { get; private set; } = new List<UserSession>();

        private Device()
        {
        }

        public Device(string installationId, DevicePlatform platform, Guid? userId = null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            InstallationId = installationId;
            Platform = platform;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            LastSeenAt = CreatedAt;
        }

        public void Register(
            Guid userId,
            DevicePlatform platform,
            string? pushToken,
            string? deviceModel,
            string? osVersion,
            string? appVersion,
            string? appBuild,
            string? locale,
            string? timezone,
            NotificationPermissionStatus? notificationPermission)
        {
            UserId = userId;
            Platform = platform;
            DeviceModel = Normalize(deviceModel);
            OsVersion = Normalize(osVersion);
            AppVersion = Normalize(appVersion);
            AppBuild = Normalize(appBuild);
            Locale = Normalize(locale);
            Timezone = Normalize(timezone);
            NotificationPermission = notificationPermission;
            IsActive = true;
            PushToken = notificationPermission == NotificationPermissionStatus.Denied
                ? null
                : Normalize(pushToken);
            Touch();
        }

        public void Update(
            DevicePlatform? platform,
            string? pushToken,
            bool updatePushToken,
            string? deviceModel,
            string? osVersion,
            string? appVersion,
            string? appBuild,
            string? locale,
            string? timezone,
            NotificationPermissionStatus? notificationPermission,
            bool? isActive)
        {
            if (platform.HasValue)
            {
                Platform = platform.Value;
            }

            if (deviceModel is not null)
            {
                DeviceModel = Normalize(deviceModel);
            }

            if (osVersion is not null)
            {
                OsVersion = Normalize(osVersion);
            }

            if (appVersion is not null)
            {
                AppVersion = Normalize(appVersion);
            }

            if (appBuild is not null)
            {
                AppBuild = Normalize(appBuild);
            }

            if (locale is not null)
            {
                Locale = Normalize(locale);
            }

            if (timezone is not null)
            {
                Timezone = Normalize(timezone);
            }

            if (updatePushToken)
            {
                PushToken = Normalize(pushToken);
            }

            if (notificationPermission.HasValue)
            {
                NotificationPermission = notificationPermission.Value;
                if (notificationPermission.Value == NotificationPermissionStatus.Denied)
                {
                    PushToken = null;
                }
            }

            if (isActive.HasValue)
            {
                IsActive = isActive.Value;
                if (!isActive.Value)
                {
                    PushToken = null;
                }
            }

            Touch();
        }

        public void Deactivate()
        {
            IsActive = false;
            PushToken = null;
            UpdatedAt = DateTime.UtcNow;
        }

        private void Touch()
        {
            var now = DateTime.UtcNow;
            LastSeenAt = now;
            UpdatedAt = now;
        }

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
