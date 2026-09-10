namespace Yakku.Application.System
{
    public static class SystemLogEventTypes
    {
        public const string OtpRequested = "OtpRequested";
        public const string OtpInvalid = "OtpInvalid";
        public const string UserRegistered = "UserRegistered";
        public const string UserLoggedIn = "UserLoggedIn";
        public const string SessionRefreshed = "SessionRefreshed";
        public const string SessionRevoked = "SessionRevoked";
        public const string PollCreated = "PollCreated";
        public const string PollClosed = "PollClosed";
        public const string PollDeleted = "PollDeleted";
        public const string VoteCast = "VoteCast";
        public const string VoteRejectedAlreadyVoted = "VoteRejectedAlreadyVoted";
        public const string DeviceRegistered = "DeviceRegistered";
        public const string DeviceUpdated = "DeviceUpdated";
        public const string DeviceUnregistered = "DeviceUnregistered";
        public const string UnhandledException = "UnhandledException";
    }
}
