using FluentValidation;
using Yakku.Application.Plans.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Plans.Validators
{
    public class UpdatePlanValidator : AbstractValidator<UpdatePlanRequest>
    {
        public UpdatePlanValidator()
        {
            RuleFor(x => x)
                .Must(x => x.Name is not null
                           || x.PriceMinor is not null
                           || x.IsActive is not null
                           || x.DurationDays is not null
                           || x.UnlockTickets is not null
                           || x.AskBackMessageLimit is not null)
                .WithMessage("Send at least one field to update.");

            RuleFor(x => x.Name).NotEmpty().MaximumLength(64).When(x => x.Name is not null);
            RuleFor(x => x.PriceMinor).GreaterThanOrEqualTo(0).When(x => x.PriceMinor is not null);
            RuleFor(x => x.DurationDays).GreaterThanOrEqualTo(1).When(x => x.DurationDays is not null);
            RuleFor(x => x.UnlockTickets)
                .Must(value => value is >= 0 || value == Plan.Unlimited)
                .WithMessage("UnlockTickets must be 0 or more, or -1 for unlimited.")
                .When(x => x.UnlockTickets is not null);
            RuleFor(x => x.AskBackMessageLimit)
                .Must(value => value is >= 1 || value == Plan.Unlimited)
                .WithMessage("AskBackMessageLimit must be 1 or more, or -1 for unlimited.")
                .When(x => x.AskBackMessageLimit is not null);
        }
    }
}
