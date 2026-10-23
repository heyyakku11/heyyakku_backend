using Yakku.Application.Sharing.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.Sharing.Interfaces
{
    public interface ISharedContentService
    {
        /// <summary>
        /// Resolves a share code to a poll or a made check. <paramref name="viewer"/> is null for anonymous callers.
        /// Throws 404 for an unknown code or a poll the caller may not read.
        /// </summary>
        Task<SharedContentResponse> GetAsync(
            string shareCode,
            VoterIdentity? viewer,
            CancellationToken cancellationToken = default);
    }

    public interface IVotingService
    {
        /// <summary>
        /// Votes on a poll or a made check, whichever the request identifies, and returns the content as the voter
        /// now sees it. One vote per voter per content item.
        /// </summary>
        Task<SharedContentResponse> VoteAsync(
            VoteRequest request,
            VoterIdentity voter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Same as <see cref="VoteAsync"/>, also returning the stored vote's id. For the legacy vote routes, which
        /// pass <paramref name="expectedType"/> so a code of the other type is a 404 there.
        /// </summary>
        Task<VoteOutcome> CastAsync(
            VoteRequest request,
            VoterIdentity voter,
            ShareContentType? expectedType = null,
            CancellationToken cancellationToken = default);
    }

    public sealed record VoteOutcome(SharedContentResponse Content, VoteReceipt Receipt);
}
