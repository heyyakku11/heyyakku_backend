using FluentValidation;
using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Validators
{
    public class CreatePollValidator : AbstractValidator<CreatePollRequest>
    {
        public CreatePollValidator()
        {
            RuleFor(x => x.Question)
                .NotEmpty()
                .MaximumLength(500);

            RuleFor(x => x.Options)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(options => options.Count >= 2)
                .WithMessage("A poll must have at least 2 options.")
                .Must(options => options.Count <= 10)
                .WithMessage("A poll can have at most 10 options.")
                .Must(HaveUniqueOptions)
                .WithMessage("Options must be unique.");

            RuleForEach(x => x.Options)
                .Must(option => !string.IsNullOrWhiteSpace(option))
                .WithMessage("Option text is required.")
                .MaximumLength(200);

            RuleFor(x => x.ExpiresAt)
                .Must(expiresAt => expiresAt is null || ToUtc(expiresAt.Value) > DateTime.UtcNow)
                .WithMessage("Expiry must be in the future.");
        }

        private static bool HaveUniqueOptions(List<string> options)
        {
            var normalized = options
                .Select(option => option.Trim())
                .ToList();

            return normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() == normalized.Count;
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
    }
}
