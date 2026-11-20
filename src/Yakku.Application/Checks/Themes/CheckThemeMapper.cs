using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Themes
{
    public static class CheckThemeMapper
    {
        public static CheckThemeConfig ConfigOf(CardTheme type) => new()
        {
            Heading = type.Name,
            BackgroundColor = type.BackgroundColor,
            LabelColor = type.LabelColor,
            Tag = type.Tag
        };

        /// <summary>The theme of a check: its card theme, or a fallback synthesized from the title.</summary>
        public static CheckThemeResponse ToResponse(Check check)
        {
            if (check.CardTheme is { } type)
            {
                return new CheckThemeResponse { CheckTypeId = type.Id, CheckType = type.Name, Config = ConfigOf(type) };
            }

            return Legacy(check.Title, "#FFFFFF");
        }

        public static CheckThemeResponse Legacy(string title, string backgroundColor)
        {
            return new CheckThemeResponse
            {
                IsLegacy = true,
                Config = new CheckThemeConfig
                {
                    Heading = title,
                    BackgroundColor = backgroundColor,
                    LabelColor = "#21191D"
                }
            };
        }
    }
}
