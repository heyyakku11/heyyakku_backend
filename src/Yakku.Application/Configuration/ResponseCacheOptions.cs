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

        public int ShareSeconds { get; set; } = 30;

        public int ReasonsSeconds { get; set; } = 30;

        public TimeSpan Plans => TimeSpan.FromSeconds(Math.Max(1, PlansSeconds));
        public TimeSpan CheckThemes => TimeSpan.FromSeconds(Math.Max(1, CheckThemesSeconds));
        public TimeSpan PersonalityTraits => TimeSpan.FromSeconds(Math.Max(1, PersonalityTraitsSeconds));
        public TimeSpan FunCatalog => TimeSpan.FromSeconds(Math.Max(1, FunCatalogSeconds));
        public TimeSpan UserCatalog => TimeSpan.FromSeconds(Math.Max(1, UserCatalogSeconds));
        public TimeSpan Share => TimeSpan.FromSeconds(Math.Max(1, ShareSeconds));
        public TimeSpan Reasons => TimeSpan.FromSeconds(Math.Max(1, ReasonsSeconds));
    }
}
