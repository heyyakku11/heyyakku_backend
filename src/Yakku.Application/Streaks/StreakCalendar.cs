namespace Yakku.Application.Streaks
{
    /// <summary>
    /// The one streak-day policy: a streak day is a UTC calendar day, derived on the server from the UTC timestamp of
    /// the business record. User timezones and device clocks are deliberately not consulted, so travelling or changing
    /// a timezone can never create, move or remove a streak day, and daylight saving has no effect.
    /// </summary>
    public static class StreakCalendar
    {
        public static DateOnly ToActivityDate(DateTime timestamp)
        {
            var utc = timestamp.Kind == DateTimeKind.Local ? timestamp.ToUniversalTime() : timestamp;
            return DateOnly.FromDateTime(utc);
        }
    }
}
