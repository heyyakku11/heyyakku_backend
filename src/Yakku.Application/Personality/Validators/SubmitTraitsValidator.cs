using FluentValidation;
using Yakku.Application.Personality.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Personality.Validators
{
    public class SubmitTraitsValidator : AbstractValidator<SubmitTraitsRequest>
    {
        public SubmitTraitsValidator()
        {
            RuleFor(x => x.TraitIds)
                .NotNull().WithMessage("traitIds is required.")
                .Must(ids => ids is null || ids.Count == PersonalityCard.TraitsPerSelection)
                .WithMessage($"Select exactly {PersonalityCard.TraitsPerSelection} traits.")
                .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
                .WithMessage("Traits must be distinct.")
                .Must(ids => ids is null || ids.All(id => id != Guid.Empty))
                .WithMessage("Trait ids must be valid ids.");
        }
    }
}
