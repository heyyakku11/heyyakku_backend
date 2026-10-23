using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Sharing
{
    public sealed record VotableOption(Guid Id, string Text, int DisplayOrder);

    public sealed record VotablePollDetails(string PollType, PollMedia? Media);

    public sealed record VotableCheckDetails(Guid CheckId, string Title, string BackgroundColor, string Sticker);

    /// <summary>
    /// What voting needs to know about a poll or a made check, independent of which one it is. Built by the
    /// repository; the poll and check tables stay separate.
    /// </summary>
    public sealed class VotableContent
    {
        public required ShareContentType ContentType { get; init; }

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public required Guid ContentId { get; init; }

        public required string ShareCode { get; init; }

        public required Guid OwnerId { get; init; }

        public required string Question { get; init; }

        /// <summary>POLL: DRAFT, ACTIVE, CLOSED, EXPIRED. CHECK: ACTIVE or INACTIVE.</summary>
        public required string Status { get; init; }

        public required bool IsOpen { get; init; }

        /// <summary>Drafts, closed and expired polls are only readable by their owner; checks are always readable.</summary>
        public required bool PubliclyReadable { get; init; }

        public DateTime? ExpiresAt { get; init; }

        public required DateTime CreatedAt { get; init; }

        public required IReadOnlyList<VotableOption> Options { get; init; }

        public VotablePollDetails? Poll { get; init; }

        public VotableCheckDetails? Check { get; init; }

        public bool IsPoll => ContentType == ShareContentType.Poll;

        /// <summary>Poll creators may vote on their own poll; a check's owner already answered it when making it.</summary>
        public bool AllowOwnerVote => IsPoll;

        /// <summary>Why nobody can vote right now (CLOSED, INACTIVE or EXPIRED), or null when voting is open.</summary>
        public string? ClosedReason(DateTime utcNow)
        {
            if (!IsOpen)
            {
                return IsPoll ? "CLOSED" : "INACTIVE";
            }

            return ExpiresAt is DateTime expiresAt && expiresAt <= utcNow ? "EXPIRED" : null;
        }
    }

    public sealed record MyVote(Guid? OptionId, string? CustomAnswer, string? Reason, DateTime VotedAt);

    /// <summary>A validated vote, ready to store.</summary>
    public sealed record NewVote(Guid? OptionId, string? CustomAnswer, string? Reason);

    /// <summary>The stored vote's identity, for the legacy vote responses that return it.</summary>
    public sealed record VoteReceipt(Guid VoteId, DateTime CreatedAt);
}
