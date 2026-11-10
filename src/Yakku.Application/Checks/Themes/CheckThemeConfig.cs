namespace Yakku.Application.Checks.Themes
{
    /// <summary>
    /// What a check renders with, as sent to clients. The type name is the heading; admins only choose the
    /// background color, heading color and an optional sticker. Clients fall back to their defaults for everything else.
    /// </summary>
    public class CheckThemeConfig
    {
        public string Heading { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string HeadingColor { get; set; } = string.Empty;

        /// <summary>Null when the type has no sticker.</summary>
        public string? StickerLabel { get; set; }
    }
}
