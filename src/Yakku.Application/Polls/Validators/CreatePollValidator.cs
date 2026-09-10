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
                .WithMessage("Question is required.")
                .MaximumLength(500);

            RuleFor(x => x.OptionType)
                .Must(BeValidOptionType)
                .WithMessage("Option type must be 'text' or 'image'.");

            RuleFor(x => x.Options)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(options => options.Count >= 2)
                .WithMessage("A poll must have at least 2 options.")
                .Must(options => options.Count <= 10)
                .WithMessage("A poll can have at most 10 options.");

            RuleFor(x => x)
                .Custom(ValidateOptionsByType);

            RuleFor(x => x.ExpiresAt)
                .Must(expiresAt => expiresAt is null || ToUtc(expiresAt.Value) > DateTime.UtcNow)
                .WithMessage("Expiry must be in the future.");
        }

        private static bool BeValidOptionType(string? optionType)
        {
            return Enum.TryParse<OptionType>(optionType?.Trim(), ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed);
        }

        private static void ValidateOptionsByType(
            CreatePollRequest request,
            ValidationContext<CreatePollRequest> context)
        {
            if (!Enum.TryParse<OptionType>(request.OptionType?.Trim(), ignoreCase: true, out var optionType)
                || !Enum.IsDefined(optionType))
            {
                return;
            }

            if (request.Options is null || request.Options.Count == 0)
            {
                return;
            }

            if (optionType == OptionType.Text)
            {
                for (var i = 0; i < request.Options.Count; i++)
                {
                    var option = request.Options[i];
                    if (string.IsNullOrWhiteSpace(option.Text))
                    {
                        context.AddFailure($"options[{i}].text", "Option text is required.");
                    }
                    else if (option.Text.Trim().Length > 200)
                    {
                        context.AddFailure($"options[{i}].text", "Option text must be 200 characters or fewer.");
                    }

                    if (option.ImageId is not null)
                    {
                        context.AddFailure(
                            $"options[{i}].imageId",
                            "Text poll options cannot include an imageId.");
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

                return;
            }

            for (var i = 0; i < request.Options.Count; i++)
            {
                var option = request.Options[i];
                if (option.ImageId is null || option.ImageId == Guid.Empty)
                {
                    context.AddFailure($"options[{i}].imageId", "Option imageId is required.");
                }

                if (!string.IsNullOrWhiteSpace(option.Text))
                {
                    context.AddFailure(
                        $"options[{i}].text",
                        "Image poll options cannot include text.");
                }
            }

            var imageIds = request.Options
                .Where(option => option.ImageId is not null && option.ImageId != Guid.Empty)
                .Select(option => option.ImageId!.Value)
                .ToList();
            if (imageIds.Count != imageIds.Distinct().Count())
            {
                context.AddFailure("options", "Image options must be unique.");
            }
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
    }
}
