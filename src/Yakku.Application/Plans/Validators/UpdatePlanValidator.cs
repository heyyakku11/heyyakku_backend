using FluentValidation;
using Yakku.Application.Plans.DTOs;

namespace Yakku.Application.Plans.Validators
{
    public class UpdatePlanValidator : AbstractValidator<UpdatePlanRequest>
    {
        public UpdatePlanValidator()
        {
            RuleFor(x => x)
                .Must(x => x.Name is not null || x.PriceMinor is not null || x.IsActive is not null)
                .WithMessage("Send at least one field to update.");

            RuleFor(x => x.Name).NotEmpty().MaximumLength(64).When(x => x.Name is not null);
            RuleFor(x => x.PriceMinor).GreaterThanOrEqualTo(0).When(x => x.PriceMinor is not null);        }
    }
}
