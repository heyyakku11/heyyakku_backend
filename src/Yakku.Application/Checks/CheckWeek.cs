namespace Yakku.Application.Checks
{
    /// <summary>Check weeks run Monday to Sunday (UTC) and are identified by their Monday.</summary>
    public static class CheckWeek
    {
        public static DateOnly StartOf(DateTime utcInstant)
        {
            return StartOf(DateOnly.FromDateTime(utcInstant.ToUniversalTime()));
        }

        public static DateOnly StartOf(DateOnly date)
        {
            var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-daysSinceMonday);
        }

        public static DateOnly Current()
        {
            return StartOf(DateTime.UtcNow);
        }
    }
}
