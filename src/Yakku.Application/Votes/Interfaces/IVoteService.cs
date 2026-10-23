using Yakku.Application.Votes.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.Votes.Interfaces
{
    public interface IVoteService
    {
        /// <summary>Casts a vote as a signed-in user.</summary>
        Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid userId,
            CastVoteRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Casts a vote as a user or a guest; <paramref name="voterId"/> is a user id or a guest id.</summary>
        Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid voterId,
            VoterRole voterRole,
            CastVoteRequest request,
            CancellationToken cancellationToken = default);
    }
}
