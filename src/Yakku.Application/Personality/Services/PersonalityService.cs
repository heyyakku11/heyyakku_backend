using FluentValidation;
using Yakku.Application.Personality.DTOs;
using Yakku.Application.Personality.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Personality.Services
{
    /// <summary>
    /// Personality card business rules. The backend alone moves a card through Draft, Collecting and Completed;
    /// every state change happens on a row-locked card inside one transaction.
    /// </summary>
    public class PersonalityService : IPersonalityService
    {
        private readonly IPersonalityRepository _repository;
        private readonly IValidator<SubmitTraitsRequest> _validator;

        public PersonalityService(IPersonalityRepository repository, IValidator<SubmitTraitsRequest> validator)
        {
            _repository = repository;
            _validator = validator;
        }

        public async Task<List<PersonalityTraitResponse>> GetTraitsAsync(CancellationToken cancellationToken = default)
        {
            var traits = await _repository.GetActiveTraitsAsync(cancellationToken);
            return traits
                .Select(trait => new PersonalityTraitResponse
                {
                    Id = trait.Id,
                    Name = trait.Name
                })
                .ToList();
        }

        public async Task<PersonalityCardResponse> GetMyCardAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var card = await _repository.GetCardByUserIdAsync(userId, cancellationToken);
            if (card is null)
            {
                // Safety net for an account that has no card (for example one created while a backfill was pending).
                await _repository.EnsureCardAsync(userId, cancellationToken);
                card = await _repository.GetCardByUserIdAsync(userId, cancellationToken)
                    ?? throw PersonalityExceptions.CardNotFound();
            }

            return ToResponse(card);
        }

        public async Task<PersonalitySelfAssessmentResponse> SubmitSelfAssessmentAsync(
            Guid userId,
            SubmitTraitsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);
            await EnsureAllActiveAsync(request.TraitIds, cancellationToken);
            await _repository.EnsureCardAsync(userId, cancellationToken);

            PersonalitySelfAssessmentResponse? response = null;
            await _repository.ExecuteInTransactionAsync(async token =>
            {
                var card = await _repository.LockCardByUserIdAsync(userId, token)
                    ?? throw PersonalityExceptions.CardNotFound();

                if (card.Status != PersonalityCardStatus.Draft)
                {
                    throw PersonalityExceptions.SelfAssessmentAlreadySubmitted();
                }

                card.SubmitSelfAssessment(request.TraitIds);
                await _repository.SaveChangesAsync(token);

                response = new PersonalitySelfAssessmentResponse
                {
                    ShareCode = card.ShareCode,
                    Status = card.Status.ToString()
                };
            }, cancellationToken);

            return response!;
        }

        public async Task<SharedPersonalityCardResponse> GetSharedCardAsync(
            string shareCode,
            Yakku.Application.Sharing.VoterIdentity? viewer,
            CancellationToken cancellationToken = default)
        {
            var card = await _repository.GetCardByShareCodeAsync(NormalizeCode(shareCode), cancellationToken)
                ?? throw PersonalityExceptions.CardNotFound();

            var isOwner = viewer is { IsUser: true } && card.UserId == viewer.Id;

            // A draft is not shared yet: only its owner knows it exists.
            if (card.Status == PersonalityCardStatus.Draft && !isOwner)
            {
                throw PersonalityExceptions.CardNotFound();
            }

            var hasVoted = !isOwner && viewer is not null
                && await _repository.HasVotedAsync(card.Id, viewer.Id, !viewer.IsUser, cancellationToken);

            return new SharedPersonalityCardResponse
            {
                CardId = card.Id,
                ShareCode = card.ShareCode,
                OwnerAnonymousName = card.User.Profile.AnonymousName,
                Status = card.Status.ToString(),
                IsOwner = isOwner,
                HasVoted = hasVoted,
                CanVote = !isOwner && !hasVoted && card.Status == PersonalityCardStatus.Collecting,
                TotalVotes = card.TotalVotes,
                RequiredVotes = card.RequiredVotes
            };
        }

        public async Task<PersonalityVoteResponse> VoteAsync(
            string shareCode,
            Yakku.Application.Sharing.VoterIdentity voter,
            SubmitTraitsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);
            await EnsureAllActiveAsync(request.TraitIds, cancellationToken);

            PersonalityVoteResponse? response = null;
            await _repository.ExecuteInTransactionAsync(async token =>
            {
                // The row lock serialises every vote on this card, so the status check, the duplicate check and the
                // counter increment below cannot interleave with another vote (the fifth vote wins, the sixth waits
                // and then sees Completed).
                var card = await _repository.LockCardByShareCodeAsync(NormalizeCode(shareCode), token)
                    ?? throw PersonalityExceptions.CardNotFound();

                if (voter.IsUser && card.UserId == voter.Id)
                {
                    throw PersonalityExceptions.CannotVoteOnOwnCard();
                }

                if (card.Status != PersonalityCardStatus.Collecting)
                {
                    throw PersonalityExceptions.NotCollectingVotes();
                }

                if (await _repository.HasVotedAsync(card.Id, voter.Id, !voter.IsUser, token))
                {
                    throw PersonalityExceptions.AlreadyVoted();
                }

                card.RegisterVote();
                _repository.AddVote(new PersonalityVote(card.Id, voter.Id, !voter.IsUser, request.TraitIds));
                await _repository.SaveChangesAsync(token);

                response = new PersonalityVoteResponse
                {
                    CardId = card.Id,
                    ShareCode = card.ShareCode,
                    CardStatus = card.Status.ToString(),
                    TotalVotes = card.TotalVotes,
                    RequiredVotes = card.RequiredVotes,
                    CompletedAt = card.CompletedAt
                };
            }, cancellationToken);

            return response!;
        }

        public async Task<PersonalityResultsResponse> GetResultsAsync(
            string shareCode,
            Guid callerId,
            CancellationToken cancellationToken = default)
        {
            var card = await _repository.GetCardByShareCodeAsync(NormalizeCode(shareCode), cancellationToken)
                ?? throw PersonalityExceptions.CardNotFound();

            if (card.UserId != callerId)
            {
                throw PersonalityExceptions.OnlyOwnerCanSeeResults();
            }

            if (card.Status != PersonalityCardStatus.Completed)
            {
                throw PersonalityExceptions.ResultsNotAvailable();
            }

            var tallies = await _repository.GetTraitTalliesAsync(card.Id, cancellationToken);
            var self = card.SelfTraits.Select(item => new SelfTraitRef(item.TraitId, item.Trait.Name, item.Trait.DisplayOrder));
            var comparison = PersonalityResultCalculator.Compare(self, tallies);

            return new PersonalityResultsResponse
            {
                CardId = card.Id,
                ShareCode = card.ShareCode,
                TotalVotes = card.TotalVotes,
                SelfSelectedTraits = comparison.SelfTraits.Select(Summary).ToList(),
                CrowdTopTraits = comparison.CrowdTop
                    .Select(tally => new PersonalityTraitCountResponse { Id = tally.TraitId, Name = tally.Name, Votes = tally.Votes })
                    .ToList(),
                CommonTraits = comparison.Common.Select(Summary).ToList(),
                SelfOnlyTraits = comparison.SelfOnly.Select(Summary).ToList(),
                CrowdOnlyTraits = comparison.CrowdOnly
                    .Select(tally => new PersonalityTraitSummary { Id = tally.TraitId, Name = tally.Name })
                    .ToList()
            };
        }

        /// <summary>Codes are short; anything empty or longer than the column cannot match a card.</summary>
        private static string NormalizeCode(string shareCode)
        {
            var code = shareCode?.Trim() ?? string.Empty;
            if (code.Length == 0 || code.Length > ShareCodeGenerator.MaxLength)
            {
                throw PersonalityExceptions.CardNotFound();
            }

            return code;
        }

        /// <summary>Every id must be an existing, active trait.</summary>
        private async Task EnsureAllActiveAsync(IReadOnlyCollection<Guid> traitIds, CancellationToken cancellationToken)
        {
            var traits = await _repository.GetTraitsByIdsAsync(traitIds, cancellationToken);
            if (traits.Count != traitIds.Count || traits.Any(trait => !trait.IsActive))
            {
                throw PersonalityExceptions.InvalidTraits();
            }
        }

        private static PersonalityTraitSummary Summary(SelfTraitRef trait) =>
            new() { Id = trait.TraitId, Name = trait.Name };

        private static PersonalityCardResponse ToResponse(PersonalityCard card)
        {
            return new PersonalityCardResponse
            {
                ShareCode = card.ShareCode,
                Status = card.Status.ToString(),
                SelfTraits = card.SelfTraits
                    .OrderBy(item => item.Trait.DisplayOrder)
                    .ThenBy(item => item.TraitId)
                    .Select(item => new PersonalityTraitSummary { Id = item.TraitId, Name = item.Trait.Name })
                    .ToList(),
                TotalVotes = card.TotalVotes,
                RequiredVotes = card.RequiredVotes,
                CompletedAt = card.CompletedAt
            };
        }
    }
}
