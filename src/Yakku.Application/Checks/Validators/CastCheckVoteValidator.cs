using FluentValidation;
using Yakku.Application.Checks.DTOs;

namespace Yakku.Application.Checks.Validators
{
    public class CastCheckVoteValidator : AbstractValidator<CastCheckVoteRequest>
    {
        public CastCheckVoteValidator()
        {
            RuleFor(x => x)
                .Must(HaveExactlyOneChoice)
                .WithMessage("Provide exactly one of checkOptionId or customAnswer.")
                .OverridePropertyName("choice");

            When(x => x.CheckOptionId is not null, () =>
            {
                RuleFor(x => x.CheckOptionId)
                    .NotEqual(Guid.Empty)
                    .WithMessage("Option id is required.");
            });

            When(x => x.CustomAnswer is not null, () =>
            {
                RuleFor(x => x.CustomAnswer)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Custom answer cannot be blank.")
                    .Must(value => value is null || value.Trim().Length <= 300)
                    .WithMessage("Custom answer must be 300 characters or fewer.");
            });

            When(x => x.Reason is not null, () =>
            {
                RuleFor(x => x.Reason)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Reason cannot be blank.")
                    .Must(value => value is null || value.Trim().Length <= 500)
                    .WithMessage("Reason must be 500 characters or fewer.");
            });
        }

        private static bool HaveExactlyOneChoice(CastCheckVoteRequest request)
        {
            var hasOption = request.CheckOptionId is not null;
            var hasCustom = !string.IsNullOrWhiteSpace(request.CustomAnswer);
            return (hasOption ? 1 : 0) + (hasCustom ? 1 : 0) == 1;
        }
    }
}
