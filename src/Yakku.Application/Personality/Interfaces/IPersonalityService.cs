using Yakku.Application.Personality.DTOs;

namespace Yakku.Application.Personality.Interfaces
{
    public interface IPersonalityService
    {
        Task<List<PersonalityTraitResponse>> GetTraitsAsync(CancellationToken cancellationToken = default);

        Task<PersonalityCardResponse> GetMyCardAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Saves whether the owner shares the completed card's final result in the community.</summary>
        Task<PersonalityCommunitySharingResponse> SetCommunitySharingAsync(
            Guid userId,
            UpdateCommunitySharingRequest? request,
            CancellationToken cancellationToken = default);

        Task<PersonalitySelfAssessmentResponse> SubmitSelfAssessmentAsync(
            Guid userId,
            SubmitTraitsRequest request,
            CancellationToken cancellationToken = default);

        Task<SharedPersonalityCardResponse> GetSharedCardAsync(
            string shareCode,
            Yakku.Application.Sharing.VoterIdentity? viewer,
            CancellationToken cancellationToken = default);

        Task<PersonalityVoteResponse> VoteAsync(
            string shareCode,
            Yakku.Application.Sharing.VoterIdentity voter,
            SubmitTraitsRequest request,
            CancellationToken cancellationToken = default);

        Task<PersonalityResultsResponse> GetResultsAsync(
            string shareCode,
            Guid callerId,
            CancellationToken cancellationToken = default);
    }
}
