using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Community.DTOs;
using Yakku.Application.Community.Interfaces;
using Yakku.Application.Personality;
using Yakku.Application.Personality.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Community.Services
{
    public class CommunityQueuedQuestionService : ICommunityQueuedQuestionService
    {
        private readonly ICommunityQueuedQuestionRepository _repository;
        private readonly IPersonalityRepository _personality;
        private readonly IValidator<UpsertCommunityQueuedQuestionRequest> _validator;

        public CommunityQueuedQuestionService(
            ICommunityQueuedQuestionRepository repository,
            IPersonalityRepository personality,
            IValidator<UpsertCommunityQueuedQuestionRequest> validator)
        {
            _repository = repository;
            _personality = personality;
            _validator = validator;
        }

        public async Task<IReadOnlyList<CommunityQueuedQuestionResponse>> ListAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var questions = await _repository.ListByUserAsync(userId, cancellationToken);
            var traits = await ResolvePersonalityTraitsAsync(userId, cancellationToken);
            return questions.Select(question => Map(question, traits)).ToList();
        }

        public async Task<CommunityQueuedQuestionResponse> CreateAsync(
            Guid userId,
            UpsertCommunityQueuedQuestionRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var count = await _repository.CountByUserAsync(userId, cancellationToken);
            if (count >= CommunityQueuedQuestion.MaxPerUser)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    $"You can queue at most {CommunityQueuedQuestion.MaxPerUser} questions.");
            }

            var tags = NormalizeTags(request.Tags);
            var entity = CommunityQueuedQuestion.Create(userId, request.Question, tags);
            _repository.Add(entity);
            await _repository.SaveChangesAsync(cancellationToken);

            var traits = await ResolvePersonalityTraitsAsync(userId, cancellationToken);
            return Map(entity, traits);
        }

        public async Task<CommunityQueuedQuestionResponse> UpdateAsync(
            Guid userId,
            Guid id,
            UpsertCommunityQueuedQuestionRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var entity = await _repository.GetByIdForUserAsync(id, userId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Queued question not found.");

            entity.Update(request.Question, NormalizeTags(request.Tags));
            await _repository.SaveChangesAsync(cancellationToken);

            var traits = await ResolvePersonalityTraitsAsync(userId, cancellationToken);
            return Map(entity, traits);
        }

        public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdForUserAsync(id, userId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Queued question not found.");

            _repository.Remove(entity);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<CommunityQueuedQuestionTraitResponse>> ResolvePersonalityTraitsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var card = await _personality.GetCardByUserIdAsync(userId, cancellationToken);
            if (card is null
                || card.Status != PersonalityCardStatus.Completed
                || !card.ShareToCommunity)
            {
                return new List<CommunityQueuedQuestionTraitResponse>();
            }

            var tallies = await _personality.GetTraitTalliesAsync(card.Id, cancellationToken);
            var self = card.SelfTraits
                .Select(trait => new SelfTraitRef(
                    trait.TraitId,
                    trait.Trait?.Name ?? string.Empty,
                    trait.Trait?.DisplayOrder ?? 0))
                .ToList();
            var comparison = PersonalityResultCalculator.Compare(self, tallies);

            return comparison.CrowdTop
                .Select(tally => new CommunityQueuedQuestionTraitResponse
                {
                    Id = tally.TraitId,
                    Name = tally.Name
                })
                .ToList();
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags) =>
            tags
                .Select(CommunityQueuedQuestionCategories.Normalize)
                .Where(tag => tag is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private static CommunityQueuedQuestionResponse Map(
            CommunityQueuedQuestion question,
            List<CommunityQueuedQuestionTraitResponse> traits) =>
            new()
            {
                Id = question.Id,
                Question = question.Question,
                Tags = question.Tags
                    .Select(tag => tag.Tag)
                    .OrderBy(CommunityQueuedQuestionCategories.SortKey)
                    .ThenBy(tag => tag, StringComparer.Ordinal)
                    .ToList(),
                CreatedAt = question.CreatedAt,
                UpdatedAt = question.UpdatedAt,
                PersonalityTraits = traits
            };
    }
}
