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

        Task<CreatorPollsPage> GetPollsAsync(
            string? cursor,
            string? contentType,
            CancellationToken cancellationToken = default);

        Task<PollResponse> GetPollDetailsAsync(
            Guid pollId,
            Guid? viewerId,
            CancellationToken cancellationToken = default);

        Task<PollResultsResponse> GetResultsAsync(
            Guid pollId,
            Guid? viewerId,
            CancellationToken cancellationToken = default);

        Task<PollResponse> PublishAsync(
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
