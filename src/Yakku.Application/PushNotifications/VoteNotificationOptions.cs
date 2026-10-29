namespace Yakku.Application.PushNotifications
{
    public static class VoteNotificationOptions
    {
        /// <summary>The first votes of a card that are each pushed on their own.</summary>
        public const int ImmediateVotes = 3;

        public static readonly TimeSpan DigestWindow = TimeSpan.FromMinutes(30);

        /// <summary>How often the dispatcher looks for due work when nothing wakes it.</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        /// <summary>A claimed row not finished by then is assumed abandoned (crash, restart) and claimable again.</summary>
        public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

        public const int BatchSize = 20;
    }
}
