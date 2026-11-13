namespace Yakku.Application.PushNotifications.Interfaces
{
    /// <summary>Used where card notifications are not wired, including unit tests of the card services.</summary>
    public sealed class NullCardLifecycleNotifications : ICardLifecycleNotifications
    {
        public static readonly NullCardLifecycleNotifications Instance = new();

        public Task NotifyPollClosedAsync(Guid pollId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyCheckClosedAsync(Guid userCheckId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void StagePersonalityReady(Guid userId, Guid cardId, int friendVotes, string shareCode)
        {
        }

        public Task NotifyExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Wake()
        {
        }
    }
}
