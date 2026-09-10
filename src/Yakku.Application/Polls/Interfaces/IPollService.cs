using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IPollService
    {
        Task<PollResponse> CreateAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task<CreatorPollsPage> GetCreatorPollsAsync(
            Guid creatorId,
            string? cursor,
            CancellationToken cancellationToken = default);

        Task<PollResponse> GetPollDetailsAsync(
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
