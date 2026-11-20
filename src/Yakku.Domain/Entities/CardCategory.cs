namespace Yakku.Domain.Entities
{
    /// <summary>Catalog of card topic categories (family, money, love, career, friendship).</summary>
    public class CardCategory
    {
        public Guid Id { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public const string Friendship = "friendship";
        public const string Career = "career";
        public const string Family = "family";
        public const string Love = "love";
        public const string Money = "money";

        public static readonly Guid FamilyId = Guid.Parse("b1000001-0000-4000-8000-000000000001");
        public static readonly Guid MoneyId = Guid.Parse("b1000001-0000-4000-8000-000000000002");
        public static readonly Guid LoveId = Guid.Parse("b1000001-0000-4000-8000-000000000003");
        public static readonly Guid CareerId = Guid.Parse("b1000001-0000-4000-8000-000000000004");
        public static readonly Guid FriendshipId = Guid.Parse("b1000001-0000-4000-8000-000000000005");

        /// <summary>Canonical lowercase codes, matching seed rows.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            Friendship,
            Career,
            Family,
            Love,
            Money
        };

        private static readonly Dictionary<string, int> Order = All
            .Select((tag, index) => (tag, index))
            .ToDictionary(item => item.tag, item => item.index, StringComparer.Ordinal);

        private static readonly HashSet<string> Allowed = new(All, StringComparer.Ordinal);

        /// <summary>Legacy alias accepted on write and normalized to <see cref="Friendship"/>.</summary>
        private const string FriendshipsLegacy = "friendships";

        private CardCategory()
        {
        }

        public static CardCategory Create(Guid id, string code, string name, int sortOrder)
        {
            return new CardCategory
            {
                Id = id,
                Code = code,
                Name = name,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static int SortKey(string tag) =>
            Order.TryGetValue(tag, out var index) ? index : int.MaxValue;

        public static bool IsAllowed(string? tag) => Normalize(tag) is not null;

        /// <summary>Returns the canonical lowercase code, or null if not allowed.</summary>
        public static string? Normalize(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            var normalized = tag.Trim().ToLowerInvariant();
            if (normalized == FriendshipsLegacy)
            {
                normalized = Friendship;
            }

            return Allowed.Contains(normalized) ? normalized : null;
        }
    }
}
