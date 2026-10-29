using Yakku.Domain.Enums;

namespace Yakku.Application.PushNotifications
{
    /// <param name="CloseOpenWindow">An open window already past its end is closed so it gets sent.</param>
    /// <param name="OpenNewWindow">A new digest window starts at this vote.</param>
    public readonly record struct VoteNotificationPlan(
        VoteNotificationKind Kind,
        bool CloseOpenWindow,
        bool OpenNewWindow);

    public static class VoteNotificationPlanner
    {
        /// <summary>
        /// Decides how the <paramref name="sequence"/>-th notifying vote of a card is delivered. The first
        /// <see cref="VoteNotificationOptions.ImmediateVotes"/> are pushed at once and the last of them starts the
        /// first digest window. Later votes join the open window; once it has ended the next vote starts a new one.
        /// </summary>
        public static VoteNotificationPlan Decide(int sequence, DateTime? openWindowEndsAt, DateTime now)
        {
            var expired = openWindowEndsAt is { } endsAt && now >= endsAt;
            var hasUsableWindow = openWindowEndsAt is not null && !expired;

            if (sequence <= VoteNotificationOptions.ImmediateVotes)
            {
                var startsWindow = sequence == VoteNotificationOptions.ImmediateVotes && !hasUsableWindow;
                return new VoteNotificationPlan(
                    VoteNotificationKind.Immediate,
                    CloseOpenWindow: startsWindow && expired,
                    OpenNewWindow: startsWindow);
            }

            return hasUsableWindow
                ? new VoteNotificationPlan(VoteNotificationKind.Digest, false, false)
                : new VoteNotificationPlan(VoteNotificationKind.Digest, expired, true);
        }
    }
}
