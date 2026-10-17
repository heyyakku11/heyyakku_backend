namespace Yakku.Application.Users.DTOs
{
    public class UserProfileResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;
        public DateTime? LoginAt { get; set; }
        public string? AvatarUrl { get; set; }

        /// <summary>The user's current plan (free, premium or yakku_love). Every user has one.</summary>
        public UserPlanResponse CurrentPlan { get; set; } = new();
    }

    public class UserPlanResponse
    {
        public Guid Id { get; set; }

        /// <summary>Stable code: free, premium or yakku_love.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Price in major units of <see cref="Currency"/> (0 for free, 49 and 129 for the paid plans).</summary>
        public decimal Price { get; set; }
        public string Currency { get; set; } = "INR";
    }
}
