namespace Yakku.Application.Configuration
{
    /// <summary>TTLs for Redis GET response cache. Bound from <c>ResponseCache</c> configuration.</summary>
    public sealed class ResponseCacheOptions
    {
        public const string SectionName = "ResponseCache";

        public bool Enabled { get; set; } = true;

        public int PlansSeconds { get; set; } = 600;

        public int CheckThemesSeconds { get; set; } = 1800;

        public int PersonalityTraitsSeconds { get; set; } = 1800;

        public int FunCatalogSeconds { get; set; } = 300;

        public int UserCatalogSeconds { get; set; } = 60;

        /// <summary>TTL for the shared live-week checks catalog (<c>checks:week:…:catalog</c>).</summary>
        public int WeeklyChecksSeconds { get; set; } = 300;

        public int ShareSeconds { get; set; } = 30;

        public int ReasonsSeconds { get; set; } = 30;

        public int UsersMeSoftSeconds { get; set; } = 30;
        public int UsersMeHardSeconds { get; set; } = 120;

        public int ListSoftSeconds { get; set; } = 15;
        public int ListHardSeconds { get; set; } = 60;

        public int NotifUnreadSoftSeconds { get; set; } = 10;
        public int NotifUnreadHardSeconds { get; set; } = 30;

        public int AskBackListSoftSeconds { get; set; } = 10;
        public int AskBackListHardSeconds { get; set; } = 45;

        public TimeSpan Plans => TimeSpan.FromSeconds(Math.Max(1, PlansSeconds));
        public TimeSpan CheckThemes => TimeSpan.FromSeconds(Math.Max(1, CheckThemesSeconds));
        public TimeSpan PersonalityTraits => TimeSpan.FromSeconds(Math.Max(1, PersonalityTraitsSeconds));
        public TimeSpan FunCatalog => TimeSpan.FromSeconds(Math.Max(1, FunCatalogSeconds));
        public TimeSpan UserCatalog => TimeSpan.FromSeconds(Math.Max(1, UserCatalogSeconds));
        public TimeSpan WeeklyChecks => TimeSpan.FromSeconds(Math.Max(1, WeeklyChecksSeconds));
        public TimeSpan Share => TimeSpan.FromSeconds(Math.Max(1, ShareSeconds));
        public TimeSpan Reasons => TimeSpan.FromSeconds(Math.Max(1, ReasonsSeconds));

        public TimeSpan UsersMeSoft => TimeSpan.FromSeconds(Math.Max(1, UsersMeSoftSeconds));
        public TimeSpan UsersMeHard => TimeSpan.FromSeconds(Math.Max(UsersMeSoftSeconds, UsersMeHardSeconds));
        public TimeSpan ListSoft => TimeSpan.FromSeconds(Math.Max(1, ListSoftSeconds));
        public TimeSpan ListHard => TimeSpan.FromSeconds(Math.Max(ListSoftSeconds, ListHardSeconds));
        public TimeSpan NotifUnreadSoft => TimeSpan.FromSeconds(Math.Max(1, NotifUnreadSoftSeconds));
        public TimeSpan NotifUnreadHard => TimeSpan.FromSeconds(Math.Max(NotifUnreadSoftSeconds, NotifUnreadHardSeconds));
        public TimeSpan AskBackListSoft => TimeSpan.FromSeconds(Math.Max(1, AskBackListSoftSeconds));
        public TimeSpan AskBackListHard => TimeSpan.FromSeconds(Math.Max(AskBackListSoftSeconds, AskBackListHardSeconds));
    }
}
