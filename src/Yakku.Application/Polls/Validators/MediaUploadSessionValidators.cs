using FluentValidation;
using Yakku.Application.Polls.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Validators
{
    public class CreateMediaUploadSessionValidator : AbstractValidator<CreateMediaUploadSessionRequest>
    {
        public CreateMediaUploadSessionValidator()
        {
            RuleFor(x => x.MediaType)
                .Must(BeMediaType)
                .WithMessage("Media type must be image, video, or audio.");

            RuleFor(x => x.ContentType)
                .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Content type is required.");

            RuleFor(x => x.FileName)
                .MaximumLength(255)
                .When(x => x.FileName is not null);

            RuleFor(x => x.FileSize)
                .GreaterThan(0)
                .When(x => x.FileSize is not null)
                .WithMessage("File size must be greater than zero.");

            RuleFor(x => x.SelectionVersion)
                .GreaterThan(0)
                .WithMessage("Selection version must be greater than zero.");

            RuleFor(x => x.PollId)
                .NotEqual(Guid.Empty)
                .When(x => x.PollId is not null)
                .WithMessage("Poll id is invalid.");
        }

        private static bool BeMediaType(string? mediaType)
        {
            return Enum.TryParse<PollMediaType>(mediaType?.Trim(), ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed);
        }
    }

    public class CompleteMediaUploadValidator : AbstractValidator<CompleteMediaUploadRequest>
    {
        public CompleteMediaUploadValidator()
        {
            RuleFor(x => x.SelectionVersion)
                .GreaterThan(0)
                .WithMessage("Selection version must be greater than zero.");
        }
    }
}
