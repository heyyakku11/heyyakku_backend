using FluentValidation;
using Yakku.Application.Sharing.DTOs;

namespace Yakku.Application.Sharing.Validators
{
    public class VoteRequestValidator : AbstractValidator<VoteRequest>
    {
        public const int MaxShareCodeLength = 64;
        public const int MaxCustomAnswerLength = 300;
        public const int MaxReasonLength = 500;

        public VoteRequestValidator()
        {
            RuleFor(x => x.ShareCode)
                .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Share code is required.")
                .Must(value => value is null || value.Trim().Length <= MaxShareCodeLength)
                .WithMessage("Share code is invalid.");

            RuleFor(x => x)
                .Must(HaveExactlyOneChoice)
                .WithMessage("Provide exactly one of optionId or customAnswer.")
                .OverridePropertyName("choice");

            When(x => x.OptionId is not null, () =>
            {
                RuleFor(x => x.OptionId)
                    .NotEqual(Guid.Empty)
                    .WithMessage("Option id is required.");
            });

            When(x => x.CustomAnswer is not null, () =>
            {
                RuleFor(x => x.CustomAnswer)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Custom answer cannot be blank.")
                    .Must(value => value is null || value.Trim().Length <= MaxCustomAnswerLength)
                    .WithMessage($"Custom answer must be {MaxCustomAnswerLength} characters or fewer.");
            });

            When(x => x.Reason is not null, () =>
            {
                RuleFor(x => x.Reason)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Reason cannot be blank.")
                    .Must(value => value is null || value.Trim().Length <= MaxReasonLength)
                    .WithMessage($"Reason must be {MaxReasonLength} characters or fewer.");
            });
        }

        private static bool HaveExactlyOneChoice(VoteRequest request)
        {
            var hasOption = request.OptionId is not null;
            var hasCustom = !string.IsNullOrWhiteSpace(request.CustomAnswer);
            return (hasOption ? 1 : 0) + (hasCustom ? 1 : 0) == 1;
        }
    }
}
