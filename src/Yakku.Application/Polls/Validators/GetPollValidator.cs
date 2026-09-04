using FluentValidation;
using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Validators
{
    public class GetPollValidator : AbstractValidator<GetPollQuery>
    {
        public GetPollValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("Poll id is required.");
        }
    }
}
