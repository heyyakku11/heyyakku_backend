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

        /// <summary>The Monday (UTC) of the week the check goes live.</summary>
        public DateOnly LiveWeekStart { get; private set; }

        public string Title { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public string BackgroundColor { get; private set; } = string.Empty;
        public string Sticker { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }

        /// <summary>
        /// The check type, which owns the visual theme. Null only for legacy checks made before types existed;
        /// those keep rendering from Title/BackgroundColor/Sticker.
        /// </summary>
        public Guid? CheckTypeId { get; private set; }
        public CheckType? CheckType { get; private set; }

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
            Guid? checkTypeId = null)
        {
            var now = DateTime.UtcNow;
            var check = new Check
            {
                Id = Guid.NewGuid(),
                LiveWeekStart = liveWeekStart,
                Title = title,
                Question = question,
                BackgroundColor = backgroundColor,
                Sticker = sticker,
                IsActive = true,
                CheckTypeId = checkTypeId,
                CreatedAt = now,
                UpdatedAt = now
            };
            check.ReplaceOptions(options);
            return check;
        }

        public void Update(
            DateOnly liveWeekStart,
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

        /// <summary>Assigns the type plus the legacy snapshot fields (the type theme at that moment). Callers allow this only while nobody has used the check.</summary>
        public void AssignType(Guid checkTypeId, string title, string backgroundColor, string sticker)
        {
            CheckTypeId = checkTypeId;
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
