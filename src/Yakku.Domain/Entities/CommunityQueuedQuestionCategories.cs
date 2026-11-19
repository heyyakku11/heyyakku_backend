namespace Yakku.Domain.Entities
{
    /// <summary>Fixed category codes for queued community questions. Stored lowercase.</summary>
    public static class CommunityQueuedQuestionCategories
    {
        public const string Friendships = "friendships";
        public const string Career = "career";
        public const string Family = "family";
        public const string Love = "love";
        public const string Money = "money";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Friendships,
            Career,
            Family,
            Love,
            Money
        };

        private static readonly Dictionary<string, int> Order = All
            .Select((tag, index) => (tag, index))
            .ToDictionary(item => item.tag, item => item.index, StringComparer.Ordinal);

        private static readonly HashSet<string> Allowed = new(All, StringComparer.Ordinal);

        public static int SortKey(string tag) =>
            Order.TryGetValue(tag, out var index) ? index : int.MaxValue;

        public static bool IsAllowed(string? tag) =>
            !string.IsNullOrWhiteSpace(tag) && Allowed.Contains(tag.Trim().ToLowerInvariant());

        /// <summary>Returns the canonical lowercase code, or null if not allowed.</summary>
        public static string? Normalize(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            var normalized = tag.Trim().ToLowerInvariant();
            return Allowed.Contains(normalized) ? normalized : null;
        }
    }
}
