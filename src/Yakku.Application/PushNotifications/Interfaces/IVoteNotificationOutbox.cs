using Yakku.Application.Sharing;

namespace Yakku.Application.PushNotifications.Interfaces
{
    public interface IVoteNotificationOutbox
    {
        /// <summary>
        /// Records the notification for a stored vote. Must run inside the vote's transaction so the vote and its
        /// notification commit or roll back together. Not called for the asker's own vote.
        /// </summary>
        Task RecordAsync(VotableContent content, VoteReceipt receipt, CancellationToken cancellationToken = default);

        /// <summary>Wakes the dispatcher after a commit so immediate pushes go out without waiting for the poll.</summary>
        void Wake();
    }
}
