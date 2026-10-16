using System.Security.Cryptography;
using System.Text;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Device
    {
        public Guid Id { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? GuestId { get; private set; }
        public string InstallationId { get; private set; } = string.Empty;
        public string? PushToken { get; private set; }

        /// <summary>SHA-256 of <see cref="PushToken"/>; backs the "one device per push token" unique index.</summary>
        public string? PushTokenHash { get; private set; }
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
        public Guest? Guest { get; private set; }
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

        public static Device ForGuest(string installationId, DevicePlatform platform, Guid guestId)
        {
            return new Device(installationId, platform) { GuestId = guestId };
        }

        public static string ComputeTokenHash(string pushToken)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pushToken)));
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
            AssignToUser(userId);
            ApplyRegistration(
                platform, pushToken, deviceModel, osVersion, appVersion, appBuild, locale, timezone,
                notificationPermission);
        }

        public void RegisterForGuest(
            Guid guestId,
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
            AssignToGuest(guestId);
            ApplyRegistration(
                platform, pushToken, deviceModel, osVersion, appVersion, appBuild, locale, timezone,
                notificationPermission);
        }

        /// <summary>An installation has exactly one owner: assigning a user releases any guest owner.</summary>
        public void AssignToUser(Guid userId)
        {
            UserId = userId;
            GuestId = null;
        }

        public void AssignToGuest(Guid guestId)
        {
            GuestId = guestId;
            UserId = null;
        }

        /// <summary>
        /// Partial update. <c>null</c> leaves a field unchanged; a blank string clears it. The platform and installation
        /// identity never change. Returns false (and writes nothing) when no value actually changed.
        /// </summary>
        public bool ApplyUpdate(
            string? pushToken,
            string? deviceModel,
            string? osVersion,
            string? appVersion,
            string? appBuild,
            string? locale,
            string? timezone,
            NotificationPermissionStatus? notificationPermission)
        {
            var changed = false;
            changed |= Set(DeviceModel, deviceModel, value => DeviceModel = value);
            changed |= Set(OsVersion, osVersion, value => OsVersion = value);
            changed |= Set(AppVersion, appVersion, value => AppVersion = value);
            changed |= Set(AppBuild, appBuild, value => AppBuild = value);
            changed |= Set(Locale, locale, value => Locale = value);
            changed |= Set(Timezone, timezone, value => Timezone = value);

            if (notificationPermission is not null && notificationPermission != NotificationPermission)
            {
                NotificationPermission = notificationPermission;
                changed = true;
            }

            // A denied permission always wins: the token is useless and must stop being used.
            var effectiveToken = NotificationPermission == NotificationPermissionStatus.Denied
                ? ""
                : pushToken;
            if (effectiveToken is not null)
            {
                var normalized = Normalize(effectiveToken);
                if (!string.Equals(normalized, PushToken, StringComparison.Ordinal))
                {
                    SetPushToken(normalized);
                    changed = true;
                }
            }

            if (changed)
            {
                Touch();
            }

            return changed;
        }

        /// <summary>Stops push delivery but keeps the record and its history.</summary>
        public void Deactivate()
        {
            if (!IsActive && PushToken is null)
            {
                return;
            }

            IsActive = false;
            SetPushToken(null);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Removes only the token (for example when it moved to a newer installation).</summary>
        public void ClearPushToken()
        {
            if (PushToken is null)
            {
                return;
            }

            SetPushToken(null);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>True when push can currently be delivered to this installation.</summary>
        public bool CanReceivePush =>
            IsActive
            && !string.IsNullOrWhiteSpace(PushToken)
            && NotificationPermission != NotificationPermissionStatus.Denied;

        private void ApplyRegistration(
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
            Platform = platform;
            DeviceModel = Normalize(deviceModel);
            OsVersion = Normalize(osVersion);
            AppVersion = Normalize(appVersion);
            AppBuild = Normalize(appBuild);
            Locale = Normalize(locale);
            Timezone = Normalize(timezone);
            NotificationPermission = notificationPermission;
            IsActive = true;
            SetPushToken(notificationPermission == NotificationPermissionStatus.Denied
                ? null
                : Normalize(pushToken));
            Touch();
        }

        private void SetPushToken(string? token)
        {
            PushToken = token;
            PushTokenHash = token is null ? null : ComputeTokenHash(token);
        }

        private static bool Set(string? current, string? incoming, Action<string?> assign)
        {
            if (incoming is null)
            {
                return false;
            }

            var normalized = Normalize(incoming);
            if (string.Equals(normalized, current, StringComparison.Ordinal))
            {
                return false;
            }

            assign(normalized);
            return true;
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
