namespace Yakku.Application.Users.DTOs
{
    /// <summary>The signed-in user, grouped by what the fields describe.</summary>
    public class UserProfileResponse
    {
        /// <summary>How the user appears in the app.</summary>
        public UserProfileDetails Profile { get; set; } = new();

        /// <summary>Who the account is: status, phone number, last login, plan and the push switch.</summary>
        public UserAccountDetails Account { get; set; } = new();

        /// <summary>Activity counts and the daily streak.</summary>
        public UserStatsDetails Stats { get; set; } = new();
    }

    public class UserProfileDetails
    {
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public class UserAccountDetails
    {
        public string Status { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime? LoginAt { get; set; }

        /// <summary>Whether the account allows push notifications (on by default).</summary>
        public bool IsPushAllowed { get; set; }

        /// <summary>The user's current plan. Every user has one.</summary>
        public UserPlanResponse CurrentPlan { get; set; } = new();
    }

    public class UserStatsDetails
    {
        /// <summary>Polls created plus checks made by the user.</summary>
        public int CardsMade { get; set; }

        /// <summary>Votes the user cast on other people's polls and checks; answering their own cards is not counted.</summary>
        public int VotesCast { get; set; }

        public UserStreakSummary Streak { get; set; } = new();
    }

    public class UserStreakSummary
    {
        /// <summary>The running streak today; zero once a whole day has been missed.</summary>
        public int CurrentStreak { get; set; }

        public int LongestStreak { get; set; }
    }

    public class UserPlanResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
