using FluentValidation;
using Yakku.Application.Categories.DTOs;

namespace Yakku.Application.Categories.Validators
{
    public class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
    {
        public CreateCategoryValidator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(name => name.Trim().Length <= 100)
                .WithMessage("Name must be 100 characters or fewer.");
        }
    }
}
