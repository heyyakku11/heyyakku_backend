using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A system-made check scheduled by an admin to go live in a given week. Content is fixed for users:
    /// they can make it but never edit it.
    /// </summary>
    public class Check
    {
        public const int MinOptions = 2;
        public const int MaxOptions = 3;

        public Guid Id { get; private set; }

        public CheckKind Kind { get; private set; }

        /// <summary>The Monday (UTC) of the week a weekly check goes live. Null for just-for-fun cards, which have no live date.</summary>
        public DateOnly? LiveWeekStart { get; private set; }

        public string Title { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public string BackgroundColor { get; private set; } = string.Empty;
        public string Sticker { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }

        /// <summary>
        /// The card theme (from the universal theme table), which owns the visual look. Null only for legacy
        /// checks made before themes existed; those keep rendering from Title/BackgroundColor/Sticker.
        /// </summary>
        public Guid? CardThemeId { get; private set; }
        public CardTheme? CardTheme { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public List<CheckOption> Options { get; private set; } = new();
        public List<UserCheck> UserChecks { get; private set; } = new();

        private Check()
        {
        }

        public static Check Create(
            DateOnly liveWeekStart,
            string title,
            string question,
            string backgroundColor,
            string sticker,
            IEnumerable<string> options,
            Guid? cardThemeId = null)
        {
            var now = DateTime.UtcNow;
            var check = new Check
            {
                Id = Guid.NewGuid(),
                Kind = CheckKind.Weekly,
                LiveWeekStart = liveWeekStart,
                Title = title,
                Question = question,
                BackgroundColor = backgroundColor,
                Sticker = sticker,
                IsActive = true,
                CardThemeId = cardThemeId,
                CreatedAt = now,
                UpdatedAt = now
            };
            check.ReplaceOptions(options);
            return check;
        }

        /// <summary>A "just for fun" card: no live week, available while active.</summary>
        public static Check CreateFun(
            string title,
            string question,
            string backgroundColor,
            IEnumerable<string> options,
            Guid cardThemeId)
        {
            var now = DateTime.UtcNow;
            var check = new Check
            {
                Id = Guid.NewGuid(),
                Kind = CheckKind.Fun,
                LiveWeekStart = null,
                Title = title,
                Question = question,
                BackgroundColor = backgroundColor,
                Sticker = string.Empty,
                IsActive = true,
                CardThemeId = cardThemeId,
                CreatedAt = now,
                UpdatedAt = now
            };
            check.ReplaceOptions(options);
            return check;
        }

        public void Update(
            DateOnly? liveWeekStart,
            string title,
            string question,
            string backgroundColor,
            string sticker,
            bool isActive)
        {
            LiveWeekStart = liveWeekStart;
            Title = title;
            Question = question;
            BackgroundColor = backgroundColor;
            Sticker = sticker;
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Assigns the theme plus the legacy snapshot fields (the theme at that moment). Callers allow this only while nobody has used the check.</summary>
        public void AssignTheme(Guid cardThemeId, string title, string backgroundColor, string sticker)
        {
            CardThemeId = cardThemeId;
            Title = title;
            BackgroundColor = backgroundColor;
            Sticker = sticker;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ReplaceOptions(IEnumerable<string> options)
        {
            var texts = options.ToList();
            if (texts.Count is < MinOptions or > MaxOptions)
            {
                throw new ArgumentException($"A check needs {MinOptions} to {MaxOptions} options.", nameof(options));
            }

            Options.Clear();
            Options.AddRange(texts.Select((text, index) => CheckOption.Create(Id, text, index + 1)));
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
