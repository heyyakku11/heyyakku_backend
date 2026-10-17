using FluentValidation;
using Yakku.Application.Checks.DTOs;

namespace Yakku.Application.Checks.Validators
{
    public class CastCheckVoteValidator : AbstractValidator<CastCheckVoteRequest>
    {
        public CastCheckVoteValidator()
        {
            RuleFor(x => x.CheckOptionId)
                .NotEmpty()
                .WithMessage("Option id is required.");

            When(x => x.Reason is not null, () =>
            {
                RuleFor(x => x.Reason)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("Reason cannot be blank.");
            });
        }
    }
}
