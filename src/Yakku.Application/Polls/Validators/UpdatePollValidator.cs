using FluentValidation;
using Yakku.Application.Polls.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Validators
{
    public class UpdatePollValidator : AbstractValidator<UpdatePollRequest>
    {
        public UpdatePollValidator()
        {
            RuleFor(x => x.Question)
                .Must(question => !string.IsNullOrWhiteSpace(question))
                .When(x => x.Question is not null)
                .WithMessage("Question is required.");

            RuleFor(x => x.ExpiresAt)
                .Must(expiresAt => expiresAt > DateTime.UtcNow)
                .When(x => x.ExpiresAt is not null)
                .WithMessage("Expiry must be in the future.");

            RuleFor(x => x.MediaUploadSessionId)
                .NotEqual(Guid.Empty)
                .When(x => x.MediaUploadSessionIdSpecified && x.MediaUploadSessionId is not null)
                .WithMessage("Upload session id is invalid.");

            RuleFor(x => x.Options)
                .Cascade(CascadeMode.Stop)
                .Must(options => options!.Count >= 2)
                .When(x => x.Options is not null)
                .WithMessage("A poll must have at least 2 options.")
                .Must(options => options!.Count <= 3)
                .When(x => x.Options is not null)
                .WithMessage("A poll can have at most 3 options.");

            RuleFor(x => x)
                .Custom(ValidateOptions)
                .When(x => x.Options is not null);
        }

        private static void ValidateOptions(
            UpdatePollRequest request,
            ValidationContext<UpdatePollRequest> context)
        {
            if (request.Options is null || request.Options.Count == 0)
            {
                return;
            }

            for (var i = 0; i < request.Options.Count; i++)
            {
                var option = request.Options[i];
                if (string.IsNullOrWhiteSpace(option.Text))
                {
                    context.AddFailure($"options[{i}].text", "Option text is required.");
                }
                else if (option.Text.Trim().Length > 500)
                {
                    context.AddFailure($"options[{i}].text", "Option text must be 500 characters or fewer.");
                }
            }

            var texts = request.Options
                .Where(option => !string.IsNullOrWhiteSpace(option.Text))
                .Select(option => option.Text!.Trim())
                .ToList();
            if (texts.Count != texts.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                context.AddFailure("options", "Options must be unique.");
            }

            var orders = request.Options.Select(option => option.DisplayOrder).ToList();
            var anyOrder = orders.Any(order => order is not null);
            var allOrders = orders.All(order => order is not null);
            if (anyOrder && !allOrders)
            {
                context.AddFailure("options", "Provide a display order for every option.");
                return;
            }

            if (!allOrders)
            {
                return;
            }

            if (orders.Any(order => order <= 0))
            {
                context.AddFailure("options", "Display order must be greater than zero.");
            }

            if (orders.Distinct().Count() != orders.Count)
            {
                context.AddFailure("options", "Display order must be unique.");
            }
        }
    }
}
