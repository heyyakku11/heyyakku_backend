using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// The visual theme of a card, from the one universal theme table. <see cref="Kind"/> says which kind of
    /// card it styles (weekly checks, just-for-fun cards, and any kind added later). For checks the name doubles
    /// as the heading shown on every check of that theme.
    /// </summary>
    public class CardTheme
    {
        public Guid Id { get; private set; }
        public CardThemeKind Kind { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string BackgroundColor { get; private set; } = string.Empty;

        /// <summary>Color of the theme label / heading text.</summary>
        public string LabelColor { get; private set; } = string.Empty;

        /// <summary>Short descriptor of what the theme is about (for example "how people see you"); null when none.</summary>
        public string? Tag { get; private set; }

        public DateTime CreatedAt { get; private set; }

        private CardTheme()
        {
        }

        public static CardTheme Create(
            CardThemeKind kind,
            string name,
            string backgroundColor,
            string labelColor,
            string? tag = null)
        {
            return new CardTheme
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Name = name,
                BackgroundColor = backgroundColor,
                LabelColor = labelColor,
                Tag = tag,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
