namespace Yakku.Application.Checks.Themes
{
    /// <summary>
    /// What a check renders with, as sent to clients. The type name is the heading; admins only choose the
    /// background color and the label color. Clients fall back to their defaults for everything else.
    /// </summary>
    public class CheckThemeConfig
    {
        public string Heading { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;

        /// <summary>Color of the check type label (the heading text).</summary>
        public string LabelColor { get; set; } = string.Empty;

        /// <summary>Short descriptor of the theme; null when it has none.</summary>
        public string? Tag { get; set; }
    }
}
