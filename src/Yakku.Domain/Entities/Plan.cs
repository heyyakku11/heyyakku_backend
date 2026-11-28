namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A subscription plan. Plans are ordered by <see cref="SortOrder"/> (free, yakku lite, yakku love). Every user is
    /// on exactly one plan. Perks and pricing are added here later.
    /// </summary>
    public class Plan
    {
        // Fixed ids so the database default, the seed rows and the code all refer to the same plans.
        public static readonly Guid FreeId = new("a1f3c0de-0000-4000-8000-000000000001");
        public static readonly Guid YakkuLiteId = new("a1f3c0de-0000-4000-8000-000000000002");
        public static readonly Guid YakkuLoveId = new("a1f3c0de-0000-4000-8000-000000000003");

        /// <summary>Catalog SKU for a one-card unlock purchase (not an activatable subscription).</summary>
        public static readonly Guid ThisCardId = new("a1f3c0de-0000-4000-8000-000000000004");

        /// <summary>Conversation top-up: adds questions to one ask back conversation. Never a user plan.</summary>
        public static readonly Guid TopUp19Id = new("a1f3c0de-0000-4000-8000-000000000005");

        /// <summary>Conversation top-up with the larger allowance. Never a user plan.</summary>
        public static readonly Guid TopUp29Id = new("a1f3c0de-0000-4000-8000-000000000006");

        public const string FreeCode = "free";
        public const string YakkuLiteCode = "yakku_lite";
        public const string YakkuLoveCode = "yakku_love";
        public const string ThisCardCode = "this_card";
        public const string TopUp19Code = "top_up_19";
        public const string TopUp29Code = "top_up_29";

        /// <summary>True for a plan code that is bought for one conversation (never activated on a user).</summary>
        public static bool IsConversationTopUpCode(string? code) => code is TopUp19Code or TopUp29Code;

        /// <summary>True for a plan code that is a user-level subscription (yakku_lite or yakku_love).</summary>
        public static bool IsUserPlanCode(string? code) => code is YakkuLiteCode or YakkuLoveCode;

        /// <summary>Sent in an update for <see cref="UnlockTickets"/> or <see cref="AskBackMessageLimit"/> to remove the limit.</summary>
        public const int Unlimited = -1;

        public Guid Id { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }

        /// <summary>
        /// Price in the smallest currency unit (paise for INR): free 0, this card 4900 (₹49),
        /// yakku lite 9900 (₹99), yakku love 29900 (₹299).
        /// </summary>
        public int PriceMinor { get; private set; }

        /// <summary>ISO 4217 currency code of the price.</summary>
        public string Currency { get; private set; } = "INR";
        public bool IsActive { get; private set; }

        /// <summary>How long one activation lasts. Null for a plan that never expires (free).</summary>
        public int? DurationDays { get; private set; }

        /// <summary>
        /// Cards a holder can unlock for Ask Back during one activation. Zero means none (free); null means no limit.
        /// </summary>
        public int? UnlockTickets { get; private set; }

        /// <summary>
        /// Questions the asker can send per reason (the voter can answer as many times). Null means no limit. The free
        /// plan's value also applies to a card unlocked on its own and to the free top-reason ask back.
        /// </summary>
        public int? AskBackMessageLimit { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        /// <summary>The users currently on this plan.</summary>
        public ICollection<UserPlan> UserPlans { get; private set; } = new List<UserPlan>();

        private Plan()
        {
        }

        /// <summary>A plan with the given perks, for code that needs one outside the database seed (such as tests).</summary>
        public static Plan Create(
            Guid id,
            string code,
            string name,
            int? durationDays,
            int? unlockTickets,
            int? askBackMessageLimit)
        {
            var now = DateTime.UtcNow;
            return new Plan
            {
                Id = id,
                Code = code,
                Name = name,
                IsActive = true,
                DurationDays = durationDays,
                UnlockTickets = unlockTickets,
                AskBackMessageLimit = askBackMessageLimit,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        /// <summary>
        /// Applies the given changes; null leaves a field as it is. For tickets and the message limit,
        /// <see cref="Unlimited"/> removes the limit.
        /// </summary>
        public void Update(
            string? name,
            int? priceMinor,
            bool? isActive,
            int? durationDays = null,
            int? unlockTickets = null,
            int? askBackMessageLimit = null)
        {
            Name = name ?? Name;
            PriceMinor = priceMinor ?? PriceMinor;
            IsActive = isActive ?? IsActive;
            DurationDays = durationDays ?? DurationDays;

            if (unlockTickets is { } tickets)
            {
                UnlockTickets = tickets == Unlimited ? null : tickets;
            }

            if (askBackMessageLimit is { } limit)
            {
                AskBackMessageLimit = limit == Unlimited ? null : limit;
            }

            UpdatedAt = DateTime.UtcNow;
        }
    }
}
