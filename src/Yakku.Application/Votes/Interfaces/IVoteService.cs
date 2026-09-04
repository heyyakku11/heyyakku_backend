using Yakku.Application.Votes.DTOs;

namespace Yakku.Application.Votes.Interfaces
{
    public interface IVoteService
    {
        Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid guestId,
            CastVoteRequest request,
            CancellationToken cancellationToken = default);
    }
}
