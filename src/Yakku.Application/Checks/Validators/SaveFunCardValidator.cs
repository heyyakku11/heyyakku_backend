using FluentValidation;
using Yakku.Application.Checks.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Validators
{
    public class SaveFunCardValidator : AbstractValidator<SaveFunCardRequest>
    {
        public SaveFunCardValidator()
        {
            RuleFor(x => x.FunCardTypeId).NotEmpty();
            RuleFor(x => x.Question).NotEmpty().MaximumLength(500);

            RuleFor(x => x.Options)
                .NotNull()
                .Must(options => options.Count is >= Check.MinOptions and <= Check.MaxOptions)
                .WithMessage($"A card needs {Check.MinOptions} to {Check.MaxOptions} options.");

            RuleForEach(x => x.Options)
                .Must(option => !string.IsNullOrWhiteSpace(option) && option.Trim().Length <= 200)
                .WithMessage("Each option must be 1 to 200 characters.");

            RuleFor(x => x.Options)
                .Must(options => options.Select(o => o.Trim().ToLowerInvariant()).Distinct().Count() == options.Count)
                .When(x => x.Options is not null && x.Options.All(o => !string.IsNullOrWhiteSpace(o)))
                .WithMessage("Options must be different from each other.");
        }
    }
}
