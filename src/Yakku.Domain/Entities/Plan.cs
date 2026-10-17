namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A subscription plan. Plans are ordered by <see cref="SortOrder"/> (free, premium, yakku love). Every user is
    /// on exactly one plan. Perks and pricing are added here later.
    /// </summary>
    public class Plan
    {
        // Fixed ids so the database default, the seed rows and the code all refer to the same plans.
        public static readonly Guid FreeId = new("a1f3c0de-0000-4000-8000-000000000001");
        public static readonly Guid PremiumId = new("a1f3c0de-0000-4000-8000-000000000002");
        public static readonly Guid YakkuLoveId = new("a1f3c0de-0000-4000-8000-000000000003");

        public const string FreeCode = "free";
        public const string PremiumCode = "premium";
        public const string YakkuLoveCode = "yakku_love";

        public Guid Id { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }

        /// <summary>Price in the smallest currency unit (paise for INR): 0 for free, 4900 for Rs 49.</summary>
        public int PriceMinor { get; private set; }

        /// <summary>ISO 4217 currency code of the price.</summary>
        public string Currency { get; private set; } = "INR";
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public ICollection<User> Users { get; private set; } = new List<User>();

        private Plan()
        {
        }
    }
}
