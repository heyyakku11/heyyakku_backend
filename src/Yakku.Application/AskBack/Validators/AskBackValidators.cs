using FluentValidation;
using Yakku.Application.AskBack.DTOs;

namespace Yakku.Application.AskBack.Validators
{
    public class CreateAskBackConversationRequestValidator : AbstractValidator<CreateAskBackConversationRequest>
    {
        public CreateAskBackConversationRequestValidator()
        {
            RuleFor(x => x.VoteId)
                .NotEqual(Guid.Empty)
                .WithMessage("Vote id is required.");
        }
    }

    public class SendAskBackMessageRequestValidator : AbstractValidator<SendAskBackMessageRequest>
    {
        public SendAskBackMessageRequestValidator()
        {
            RuleFor(x => x.ClientMessageId)
                .NotEqual(Guid.Empty)
                .WithMessage("Client message id is required.");

            RuleFor(x => x.Content)
                .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Message cannot be empty.")
                .Must(value => value is null || value.Trim().Length <= AskBackOptions.MaxMessageLength)
                .WithMessage($"Message cannot be longer than {AskBackOptions.MaxMessageLength} characters.");
        }
    }

    public class MarkAskBackReadRequestValidator : AbstractValidator<MarkAskBackReadRequest>
    {
        public MarkAskBackReadRequestValidator()
        {
            RuleFor(x => x.LastReadMessageId)
                .NotEqual(Guid.Empty)
                .WithMessage("Last read message id is required.");
        }
    }

    public class BlockAskBackRequestValidator : AbstractValidator<BlockAskBackRequest>
    {
        public BlockAskBackRequestValidator()
        {
            RuleFor(x => x.Reason)
                .Must(value => value is null || value.Trim().Length <= AskBackOptions.MaxReportReasonLength)
                .WithMessage($"Reason cannot be longer than {AskBackOptions.MaxReportReasonLength} characters.");
        }
    }
}
