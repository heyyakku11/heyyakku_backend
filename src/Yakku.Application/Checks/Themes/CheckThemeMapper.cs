using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Themes
{
    public static class CheckThemeMapper
    {
        public static CheckThemeConfig ConfigOf(CheckType type) => new()
        {
            Heading = type.Name,
            BackgroundColor = type.BackgroundColor,
            StickerLabel = type.Sticker
        };

        /// <summary>The theme of a check: its check type, or one synthesized from the legacy fields.</summary>
        public static CheckThemeResponse ToResponse(Check check)
        {
            if (check.CheckType is { } type)
            {
                return new CheckThemeResponse { CheckTypeId = type.Id, CheckType = type.Name, Config = ConfigOf(type) };
            }

            return Legacy(check.Title, check.BackgroundColor, check.Sticker);
        }

        public static CheckThemeResponse Legacy(string title, string backgroundColor, string sticker)
        {
            return new CheckThemeResponse
            {
                IsLegacy = true,
                Config = new CheckThemeConfig
                {
                    Heading = title,
                    BackgroundColor = backgroundColor,
                    StickerLabel = string.IsNullOrWhiteSpace(sticker) ? null : sticker
                }
            };
        }
    }
}
