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

        /// <summary>True when the caller saved this card to their saved list. Always false for signed-out callers.</summary>
        public bool IsSaved { get; set; }

        /// <summary>The theme name (for example "Just for fun").</summary>
        public string? CheckType { get; set; }
        public CheckThemeResponse Theme { get; set; } = new();
    }

    /// <summary>Admin create body. Visuals come from the fun card type (theme), never from the request.</summary>
    public class SaveFunCardRequest
    {
        /// <summary>Id of the fun card type (theme) this card uses.</summary>
        public Guid FunCardTypeId { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    /// <summary>One fun card type (theme) with its cards. In GET fun-cards the cards are capped at a preview of 4.</summary>
    public class AdminFunCardGroupResponse
    {
        public AdminFunCardGroupTheme Theme { get; set; } = new();

        /// <summary>How many cards this type has in all, active or not. <see cref="Cards"/> may hold fewer (a preview).</summary>
        public int TotalCount { get; set; }

        public List<AdminFunCardItemResponse> Cards { get; set; } = new();
    }

    public class AdminFunCardGroupTheme
    {
        public Guid FunCardTypeId { get; set; }
        public string Heading { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string LabelColor { get; set; } = string.Empty;
        public string? Tag { get; set; }
    }

    public class AdminFunCardCreatedResponse
    {
        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class AdminFunCardItemResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<CheckOptionResponse> Options { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
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

namespace Yakku.Application.Checks.DTOs
{
    /// <summary>One fun card type for the app: its cards the caller has not made yet, capped at a preview.</summary>
    public class FunCardTypeResponse
    {
        public Guid? TypeId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>All cards of this type the caller can still make. <see cref="Cards"/> holds at most the preview size.</summary>
        public int TotalCount { get; set; }

        public List<FunCardResponse> Cards { get; set; } = new();
    }
}
