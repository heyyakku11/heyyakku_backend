namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A kind of check (for example "Vibe Check") together with its single visual theme. Defined by an admin;
    /// the name doubles as the heading shown on every check of this type.
    /// </summary>
    public class CheckType
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string BackgroundColor { get; private set; } = string.Empty;

        /// <summary>Color of the check type label / heading text.</summary>
        public string LabelColor { get; private set; } = string.Empty;

        /// <summary>Optional sticker text; null when the type has no sticker.</summary>
        public string? Sticker { get; private set; }

        private CheckType()
        {
        }

        public static CheckType Create(string name, string backgroundColor, string labelColor, string? sticker = null)
        {
            return new CheckType
            {
                Id = Guid.NewGuid(),
                Name = name,
                BackgroundColor = backgroundColor,
                LabelColor = labelColor,
                Sticker = sticker
            };
        }
    }
}
