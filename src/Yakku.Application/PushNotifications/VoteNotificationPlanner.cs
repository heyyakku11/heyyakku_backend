using Yakku.Domain.Enums;

namespace Yakku.Application.PushNotifications
{
    public static class VoteNotificationPlanner
    {
        /// <summary>
        /// How the <paramref name="sequence"/>-th notifying vote of a card starts. The first
        /// <paramref name="immediateVoteLimit"/> votes are pushed on their own at once, whatever the voting
        /// frequency. The sequence is the card's durable counter, taken in the vote's own transaction, so this never
        /// depends on Redis. Every later vote waits, durably, to be classified from the card's recent frequency.
        /// </summary>
        public static VoteNotificationKind Decide(int sequence, int immediateVoteLimit)
        {
            return sequence <= immediateVoteLimit
                ? VoteNotificationKind.Immediate
                : VoteNotificationKind.Undecided;
        }
    }
}
