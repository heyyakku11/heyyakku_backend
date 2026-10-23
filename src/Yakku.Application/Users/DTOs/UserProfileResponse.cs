namespace Yakku.Application.Users.DTOs
{
    public class UserProfileResponse
    {
        public string Status { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;
        public DateTime? LoginAt { get; set; }
        public string? AvatarUrl { get; set; }

        /// <summary>The user's current plan. Every user has one.</summary>
        public UserPlanResponse CurrentPlan { get; set; } = new();
    }

    public class UserPlanResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
