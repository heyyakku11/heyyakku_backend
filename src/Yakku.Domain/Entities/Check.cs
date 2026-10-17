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

        /// <summary>Order among the checks that go live in the same week.</summary>
        public int SequenceNo { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public string BackgroundColor { get; private set; } = string.Empty;
        public string Sticker { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public List<CheckOption> Options { get; private set; } = new();
        public List<UserCheck> UserChecks { get; private set; } = new();

        private Check()
        {
        }

        public static Check Create(
            DateOnly liveWeekStart,
            int sequenceNo,
            string title,
            string question,
            string backgroundColor,
            string sticker,
            IEnumerable<string> options)
        {
            var now = DateTime.UtcNow;
            var check = new Check
            {
                Id = Guid.NewGuid(),
                LiveWeekStart = liveWeekStart,
                SequenceNo = sequenceNo,
                Title = title,
                Question = question,
                BackgroundColor = backgroundColor,
                Sticker = sticker,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            check.ReplaceOptions(options);
            return check;
        }

        public void Update(
            DateOnly liveWeekStart,
            int sequenceNo,
            string title,
            string question,
            string backgroundColor,
            string sticker,
            bool isActive)
        {
            LiveWeekStart = liveWeekStart;
            SequenceNo = sequenceNo;
            Title = title;
            Question = question;
            BackgroundColor = backgroundColor;
            Sticker = sticker;
            IsActive = isActive;
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
