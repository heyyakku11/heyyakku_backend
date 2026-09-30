using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Interfaces
{
    public interface ISavedPollService
    {
        Task<SavedPollStateResponse> SaveAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task<SavedPollStateResponse> UnsaveAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task<CreatorPollsPage> ListAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default);
    }
}
