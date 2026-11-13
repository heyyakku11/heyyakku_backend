using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IPollService
    {
        Task<PollResponse> CreateAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task<PollResponse> UpdateAsync(
            Guid pollId,
            UpdatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task<PollResultsResponse> GetResultsAsync(
            Guid pollId,
            Guid? viewerId,
            CancellationToken cancellationToken = default);

        Task<PollResponse> PublishAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        /// <summary>Opens one of the owner's closed polls again. Status returns to active, closedAt is cleared, and the expiry becomes the close time plus 24 hours. 404 when it is not theirs, 409 when it is not closed.</summary>
        Task<PollResponse> ResharePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task<PollResponse> ClosePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task DeletePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default);
    }
}
