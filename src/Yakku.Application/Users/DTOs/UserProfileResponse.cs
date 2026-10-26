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
        public bool IsPushAllowed { get; set; }

        /// <summary>Polls created plus checks made by the user.</summary>
        public int CardsMade { get; set; }

        /// <summary>Votes the user cast on other people's polls and checks; answering their own cards is not counted.</summary>
        public int VotesCast { get; set; }

        public UserStreakSummary Streak { get; set; } = new();

        /// <summary>The user's current plan. Every user has one.</summary>
        public UserPlanResponse CurrentPlan { get; set; } = new();
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
