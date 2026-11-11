using Yakku.Application.Checks.Themes;

namespace Yakku.Application.Checks.DTOs
{
    /// <summary>
    /// A "just for fun" card as listed to the app. These cards have no live or expiry date: every active card is
    /// listed, and <see cref="Used"/> says whether the caller already made it their own.
    /// </summary>
    public class FunCardResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public List<CheckOptionResponse> Options { get; set; } = new();
        public bool Used { get; set; }

        /// <summary>The theme name (for example "Just for fun").</summary>
        public string? CheckType { get; set; }
        public CheckThemeResponse Theme { get; set; } = new();
    }

    /// <summary>Admin create/update body. Visuals always come from the just-for-fun theme, never from the request.</summary>
    public class SaveFunCardRequest
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class AdminFunCardResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<CheckOptionResponse> Options { get; set; } = new();
        public CheckThemeResponse Theme { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
