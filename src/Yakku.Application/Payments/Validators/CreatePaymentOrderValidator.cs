using FluentValidation;
using Yakku.Application.Payments.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Payments.Validators
{
    public class CreatePaymentOrderValidator : AbstractValidator<CreatePaymentOrderRequest>
    {
        public CreatePaymentOrderValidator()
        {
            RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(32);
            RuleFor(x => x.IdempotencyKey)
                .NotEmpty()
                .MaximumLength(128)
                .Matches(@"^[A-Za-z0-9._\-:/]+$")
                .WithMessage("IdempotencyKey may contain letters, digits, and . _ - : / only.");

            RuleFor(x => x.ContentType)
                .Must(value => value is null
                               || value.Equals("poll", StringComparison.OrdinalIgnoreCase)
                               || value.Equals("check", StringComparison.OrdinalIgnoreCase))
                .WithMessage("contentType must be poll or check.");

            RuleFor(x => x)
                .Must(x => (x.ContentType is null && x.ContentId is null)
                           || (x.ContentType is not null && x.ContentId is not null && x.ContentId != Guid.Empty))
                .WithMessage("contentType and contentId must both be set for a card purchase, or both omitted.");

            // A top-up is bought for one conversation and has no card; every other purchase has no conversation.
            RuleFor(x => x.ConversationId)
                .NotNull()
                .NotEqual(Guid.Empty)
                .When(x => Plan.IsConversationTopUpCode(x.PlanCode))
                .WithMessage("conversationId is required to top up a conversation.");

            RuleFor(x => x)
                .Must(x => x.ContentType is null && x.ContentId is null)
                .When(x => Plan.IsConversationTopUpCode(x.PlanCode))
                .WithMessage("A conversation top-up has no card: remove contentType and contentId.");

            RuleFor(x => x.ConversationId)
                .Null()
                .When(x => !Plan.IsConversationTopUpCode(x.PlanCode))
                .WithMessage("conversationId is only used to top up a conversation.");
        }
    }
}
