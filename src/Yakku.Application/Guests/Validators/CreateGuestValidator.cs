using FluentValidation;
using Yakku.Application.Guests.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Guests.Validators
{
    public class CreateGuestValidator : AbstractValidator<CreateGuestRequest>
    {
        public CreateGuestValidator()
        {
            RuleFor(x => x.IdempotencyKey)
                .NotEmpty()
                .MaximumLength(Guest.MaxIdempotencyKeyLength)
                .Matches(@"^[A-Za-z0-9._\-:/]+$")
                .WithMessage("IdempotencyKey may contain letters, digits, and . _ - : / only.");
        }
    }
}
