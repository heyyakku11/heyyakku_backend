namespace Yakku.Application.Users.DTOs
{
    /// <summary>The signed-in user, grouped by what the fields describe.</summary>
    public class UserProfileResponse
    {
        /// <summary>How the user appears in the app.</summary>
        public UserProfileDetails Profile { get; set; } = new();

        /// <summary>Who the account is: the current plan.</summary>
        public UserAccountDetails Account { get; set; } = new();

        /// <summary>Activity counts and the daily streak.</summary>
        public UserStatsDetails Stats { get; set; } = new();
    }

    public class UserProfileDetails
    {
        public string AnonymousName { get; set; } = string.Empty;
    }

    public class UserAccountDetails
    {
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
        /// <summary>Plan code (free, yakku_lite, yakku_love).</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>When the current activation of a paid plan began; null on the free plan.</summary>
        public DateTime? ActivatedAt { get; set; }

        /// <summary>When the paid plan ends (or ended); null on the free plan.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>A paid plan was bought and its period has ended, so the user is back on the free perks.</summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// Cards unlockable on this activation; null means unlimited (Love).
        /// Sourced from the plan row — never invented by the client.
        /// </summary>
        public int? UnlockTickets { get; set; }

        /// <summary>
        /// Plan-sourced card unlocks used on the current entitlement (Lite tickets / Love unlocks so far).
        /// </summary>
        public int CardsUnlocked { get; set; }
    }
}
