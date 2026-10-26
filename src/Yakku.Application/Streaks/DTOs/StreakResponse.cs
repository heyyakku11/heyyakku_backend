namespace Yakku.Application.Streaks.DTOs
{
    public class StreakResponse
    {
        /// <summary>The running streak today; zero once a whole day has been missed.</summary>
        public int CurrentStreak { get; set; }

        public int LongestStreak { get; set; }

        /// <summary>The last UTC day with qualifying activity, or null if there has been none.</summary>
        public DateOnly? LastActivityDate { get; set; }

        /// <summary>True when the user has already earned today's (UTC) streak day.</summary>
        public bool IsActiveToday { get; set; }

        /// <summary>The server's current UTC date; streak days are UTC calendar days.</summary>
        public DateOnly Today { get; set; }
    }

    public class StreakHistoryResponse
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public List<DateOnly> ActiveDates { get; set; } = [];
    }
}
