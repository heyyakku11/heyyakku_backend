using FluentValidation;
using Yakku.Application.Community.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Community.Validators
{
    public class UpsertCommunityQueuedQuestionValidator : AbstractValidator<UpsertCommunityQueuedQuestionRequest>
    {
        public UpsertCommunityQueuedQuestionValidator()
        {
            RuleFor(x => x.Question)
                .Must(question => !string.IsNullOrWhiteSpace(question))
                .WithMessage("Question is required.")
                .Must(question => question is null || question.Trim().Length <= CommunityQueuedQuestion.QuestionMaxLength)
                .WithMessage($"Question must be {CommunityQueuedQuestion.QuestionMaxLength} characters or fewer.");

            RuleFor(x => x.Tags)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("tags is required.")
                .Must(tags => tags is not null && tags.Count >= 1)
                .WithMessage("Select at least one category tag.")
                .Must(tags => tags is null || tags.All(CommunityQueuedQuestionCategories.IsAllowed))
                .WithMessage("One or more tags are not allowed.")
                .Must(tags =>
                {
                    if (tags is null)
                    {
                        return true;
                    }

                    var normalized = tags
                        .Select(CommunityQueuedQuestionCategories.Normalize)
                        .Where(tag => tag is not null)
                        .Cast<string>()
                        .ToList();
                    return normalized.Count == normalized.Distinct(StringComparer.Ordinal).Count();
                })
                .WithMessage("Tags must be unique.");
        }
    }
}
