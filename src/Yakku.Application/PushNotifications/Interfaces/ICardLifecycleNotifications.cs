namespace Yakku.Application.PushNotifications.Interfaces
{
    /// <summary>
    /// Inbox rows for a card ending and for a personality card completing. Vote-by-vote rows are written by the
    /// vote outbox. These run after the card change is stored, and a failure here does not undo that change.
    /// </summary>
    public interface ICardLifecycleNotifications
    {
        Task NotifyPollClosedAsync(Guid pollId, CancellationToken cancellationToken = default);

        Task NotifyCheckClosedAsync(Guid userCheckId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds the personality result row on the current context, without saving, so it commits with the vote
        /// that completed the card. Call <see cref="Wake"/> after that transaction commits.
        /// </summary>
        void StagePersonalityReady(Guid userId, Guid cardId, int friendVotes, string shareCode);

        /// <summary>Writes a close row for cards whose time ran out and that do not already have one.</summary>
        Task NotifyExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Wakes the push dispatcher after a commit.</summary>
        void Wake();
    }
}
