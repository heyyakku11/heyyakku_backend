using FluentValidation;

namespace Yakku.Application.Checks.Themes
{
    public class CreateCheckThemeValidator : AbstractValidator<CreateCheckThemeRequest>
    {
        private const string HexPattern = "^#[0-9A-Fa-f]{6}$";

        public CreateCheckThemeValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.BackgroundColor).Matches(HexPattern).WithMessage("Background color must be a hex color like #FFE0E8.");
            RuleFor(x => x.Sticker!).MaximumLength(100).When(x => x.Sticker is not null);
        }
    }
}
