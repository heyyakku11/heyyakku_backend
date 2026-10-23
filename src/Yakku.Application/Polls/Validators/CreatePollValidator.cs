using FluentValidation;
using Yakku.Application.Polls.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Validators
{
    public class CreatePollValidator : AbstractValidator<CreatePollRequest>
    {
        public CreatePollValidator()
        {
            RuleFor(x => x.Question)
                .Must(question => !string.IsNullOrWhiteSpace(question))
                .WithMessage("Question is required.");

            RuleFor(x => x.ContentType)
                .Must(BeValidContentType)
                .WithMessage("Content type must be question, image, video, or audio.");

            RuleFor(x => x.ExpiresAt)
                .Must(expiresAt => expiresAt > DateTime.UtcNow)
                .When(x => x.ExpiresAt is not null)
                .WithMessage("Expiry must be in the future.");

            RuleFor(x => x.MediaUploadSessionId)
                .NotEqual(Guid.Empty)
                .When(x => x.MediaUploadSessionId is not null)
                .WithMessage("Upload session id is invalid.");

            RuleFor(x => x.MediaUploadSessionId)
                .NotNull()
                .When(RequiresMedia)
                .WithMessage("A completed media upload is required for this poll.");

            RuleFor(x => x.MediaUploadSessionId)
                .Null()
                .When(IsQuestionPoll)
                .WithMessage("Question polls cannot have media.");

            RuleFor(x => x.Options)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(options => options.Count >= 2)
                .WithMessage("A poll must have at least 2 options.")
                .Must(options => options.Count <= 3)
                .WithMessage("A poll can have at most 3 options.");

            RuleFor(x => x)
                .Custom(ValidateOptions);
        }

        private static bool BeValidContentType(string? contentType)
        {
            return Enum.TryParse<PollContentType>(contentType?.Trim(), ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed);
        }

        private static bool RequiresMedia(CreatePollRequest request)
        {
            return Enum.TryParse<PollContentType>(request.ContentType?.Trim(), ignoreCase: true, out var parsed)
                   && parsed is PollContentType.Image or PollContentType.Video or PollContentType.Audio;
        }

        private static bool IsQuestionPoll(CreatePollRequest request)
        {
            return Enum.TryParse<PollContentType>(request.ContentType?.Trim(), ignoreCase: true, out var parsed)
                   && parsed == PollContentType.Question;
        }

        private static void ValidateOptions(
            CreatePollRequest request,
            ValidationContext<CreatePollRequest> context)
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
