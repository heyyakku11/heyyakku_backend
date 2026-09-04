using FluentValidation;
using Yakku.Application.Votes.DTOs;

namespace Yakku.Application.Votes.Validators
{
    public class CastVoteValidator : AbstractValidator<CastVoteRequest>
    {
        public CastVoteValidator()
        {
            RuleFor(x => x)
                .Must(HaveExactlyOneChoice)
                .WithMessage("Provide either optionId or customOption, not both.");

            When(x => x.OptionId is not null, () =>
            {
                RuleFor(x => x.OptionId)
                    .NotEqual(Guid.Empty)
                    .WithMessage("Option id is required.");
            });

            When(x => !string.IsNullOrWhiteSpace(x.CustomOption), () =>
            {
                RuleFor(x => x.CustomOption)
                    .MaximumLength(200);
            });

            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .When(x => x.Reason is not null);
        }

        private static bool HaveExactlyOneChoice(CastVoteRequest request)
        {
            var hasOption = request.OptionId is not null;
            var hasCustom = !string.IsNullOrWhiteSpace(request.CustomOption);
            return hasOption ^ hasCustom;
        }
    }
}
