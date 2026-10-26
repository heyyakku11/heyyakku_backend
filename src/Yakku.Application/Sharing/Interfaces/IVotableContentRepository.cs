using Yakku.Application.Polls;

namespace Yakku.Application.Sharing.Interfaces
{
    public interface IVotableContentRepository
    {
        /// <summary>Resolves a share code (poll or made check) through the share code registry.</summary>
        Task<VotableContent?> FindByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Raw counts for the content. <see cref="PollVoteCountSnapshot.OtherVotes"/> excludes the owner's own vote.
        /// </summary>
        Task<PollVoteCountSnapshot> GetTallyAsync(VotableContent content, CancellationToken cancellationToken = default);

        Task<MyVote?> GetVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores the vote atomically. The content row is locked (FOR SHARE) and its open state re-checked inside the
        /// transaction, so a vote cannot land after a close. Throws AlreadyVoted (409) when the voter already voted
        /// and a 400 when the content is no longer open.
        /// <paramref name="afterAdded"/> runs inside the same transaction after the vote is stored and before it
        /// commits; if it throws, the vote is rolled back with it.
        /// </summary>
        Task<VoteReceipt> AddVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            NewVote vote,
            Func<VoteReceipt, CancellationToken, Task>? afterAdded = null,
            CancellationToken cancellationToken = default);
    }
}
