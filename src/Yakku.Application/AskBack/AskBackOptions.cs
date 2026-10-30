namespace Yakku.Application.AskBack
{
    public static class AskBackOptions
    {
        public const int MaxMessageLength = 2000;
        public const int MaxReportReasonLength = 500;
        public const int PreviewLength = 80;

        public const int ConversationPageSize = 20;
        public const int MessagePageSize = 30;
        public const int MaxPageSize = 50;

        /// <summary>
        /// A push waits this long after the message. A recipient who is looking at the conversation joins its
        /// SignalR group and never gets one; this also leaves room for a read receipt to arrive first.
        /// </summary>
        public static readonly TimeSpan PushDelay = TimeSpan.FromSeconds(3);

        /// <summary>How often the push worker looks for due rows.</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        /// <summary>A claimed row not finished by then is assumed abandoned (crash, restart) and claimable again.</summary>
        public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

        public const int BatchSize = 20;

        public const string DeletedUserName = "Deleted user";
    }
}
