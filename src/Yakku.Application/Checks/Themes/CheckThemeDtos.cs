namespace Yakku.Application.Checks.Themes
{
    /// <summary>Admin body to define a check theme (which is also the check type).</summary>
    public class CreateCheckThemeRequest
    {
        /// <summary>The check type name; shown as the heading of every check of this type.</summary>
        public string Name { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string LabelColor { get; set; } = string.Empty;

        /// <summary>Optional short descriptor of the theme (for example "how people see you").</summary>
        public string? Tag { get; set; }
    }

    /// <summary>A check theme as defined by an admin. The id is what admins pick when creating a check.</summary>
    public class CheckThemeDefinitionResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string LabelColor { get; set; } = string.Empty;
        public string? Tag { get; set; }
    }

    /// <summary>The theme a check renders with. IsLegacy means it was derived from a check made before types existed.</summary>
    public class CheckThemeResponse
    {
        public Guid? CheckTypeId { get; set; }

        /// <summary>The check type name; null for legacy checks.</summary>
        public string? CheckType { get; set; }
        public bool IsLegacy { get; set; }
        public CheckThemeConfig Config { get; set; } = new();
    }
}
