using FluentValidation;
using Yakku.Application.Votes.DTOs;

namespace Yakku.Application.Votes.Validators
{
    public class CastVoteValidator : AbstractValidator<CastVoteRequest>
    {
        public CastVoteValidator()
        {
            RuleFor(x => x.PollId)
                .NotEmpty()
                .WithMessage("Poll id is required.");

            RuleFor(x => x)
                .Must(HaveExactlyOneChoice)
                .WithMessage("Provide exactly one of pollOptionId or customAnswer.")
                .OverridePropertyName("choice");

            When(x => x.PollOptionId is not null, () =>
            {
                RuleFor(x => x.PollOptionId)
                    .NotEqual(Guid.Empty)
                    .WithMessage("Option id is required.");
            });

            When(x => x.CustomAnswer is not null, () =>
            {
                RuleFor(x => x.CustomAnswer)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Custom answer cannot be blank.");
            });

            When(x => x.Reason is not null, () =>
            {
                RuleFor(x => x.Reason)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Reason cannot be blank.");
            });
        }

        private static bool HaveExactlyOneChoice(CastVoteRequest request)
        {
            var hasOption = request.PollOptionId is not null;
            var hasCustom = !string.IsNullOrWhiteSpace(request.CustomAnswer);
            var count = (hasOption ? 1 : 0) + (hasCustom ? 1 : 0);
            return count == 1;
        }
    }
}
