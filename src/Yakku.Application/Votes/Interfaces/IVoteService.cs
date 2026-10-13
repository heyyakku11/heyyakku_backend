using Yakku.Application.Votes.DTOs;

namespace Yakku.Application.Votes.Interfaces
{
    public interface IVoteService
    {
        Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid userId,
            CastVoteRequest request,
            CancellationToken cancellationToken = default);
    }
}
