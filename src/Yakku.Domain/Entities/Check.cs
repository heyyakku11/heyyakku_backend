namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A system-made check scheduled by an admin. Weekly checks have a <see cref="LiveWeekStart"/>;
    /// just-for-fun cards leave it null. Visual look comes from <see cref="CardTheme"/>.
    /// </summary>
    public class Check
    {
        public const int MinOptions = 2;
        public const int MaxOptions = 3;

        public Guid Id { get; private set; }

        /// <summary>The Monday (UTC) of the week a weekly check goes live. Null for just-for-fun cards.</summary>
        public DateOnly? LiveWeekStart { get; private set; }

        public string Title { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }

        /// <summary>The card theme that owns the visual look. Required for new checks.</summary>
        public Guid? CardThemeId { get; private set; }
        public CardTheme? CardTheme { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public List<CheckOption> Options { get; private set; } = new();
        public List<UserCheck> UserChecks { get; private set; } = new();

        /// <summary>True when this check has no live week (just-for-fun).</summary>
        public bool IsFun => LiveWeekStart is null;

        private Check()
        {
        }

        public static Check Create(
            DateOnly liveWeekStart,
            string title,
            string question,
            IEnumerable<string> options,
            Guid cardThemeId)
        {
            var now = DateTime.UtcNow;
            var check = new Check
            {
                Id = Guid.NewGuid(),
                LiveWeekStart = liveWeekStart,
                Title = title,
                Question = question,
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
            IEnumerable<string> options,
            Guid cardThemeId)
        {
            var now = DateTime.UtcNow;
            var check = new Check
            {
                Id = Guid.NewGuid(),
                LiveWeekStart = null,
                Title = title,
                Question = question,
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
            bool isActive)
        {
            LiveWeekStart = liveWeekStart;
            Title = title;
            Question = question;
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Assigns the theme and snapshots its name into Title. Allowed only while nobody has used the check.</summary>
        public void AssignTheme(Guid cardThemeId, string title)
        {
            CardThemeId = cardThemeId;
            Title = title;
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
