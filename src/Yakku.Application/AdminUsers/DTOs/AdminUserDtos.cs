namespace Yakku.Application.AdminUsers.DTOs
{
    public class AdminUserResponse
    {
        public Guid Id { get; set; }
        public string AnonymousName { get; set; } = string.Empty;

        /// <summary>The phone number with its country code, as stored (for example +919876543210).</summary>
        public string PhoneNumber { get; set; } = string.Empty;

        public AdminUserPlanResponse Plan { get; set; } = new();

        /// <summary>active or disabled.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Always null today: users have no avatar yet.</summary>
        public string? AvatarUrl { get; set; }

        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminUserPlanResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class AdminGuestResponse
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>active, or expired once the guest's expiry has passed.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>The app installation last seen for this guest; null when none was registered.</summary>
        public AdminGuestDeviceResponse? Device { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime? LastSeenAt { get; set; }

        /// <summary>Votes this guest cast on polls and checks.</summary>
        public int CardVotes { get; set; }
    }

    public class AdminGuestDeviceResponse
    {
        public string Platform { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
    }

    /// <summary>One page of rows from the repository, with the total across all pages.</summary>
    public class AdminPage<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
