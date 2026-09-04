using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IPollService
    {
        Task<CreatePollResponse> CreateAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default);
        Task<PollResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
