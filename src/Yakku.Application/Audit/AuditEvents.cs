namespace Yakku.Application.Audit
{
    public static class AuditEvents
    {
        public static class Auth
        {
            public const string OtpRequested = "otp.requested";
            public const string OtpVerifyFailed = "otp.verify_failed";
            public const string Registered = "user.registered";
            public const string LoggedIn = "login.succeeded";
            public const string LoginFailed = "login.failed";
            public const string SessionCreated = "session.created";
            public const string SessionEvicted = "session.evicted";
            public const string TokenRefreshed = "token.refreshed";
            public const string TokenRefreshFailed = "token.refresh_failed";
            public const string SessionRevoked = "session.revoked";
            public const string SessionRevokeFailed = "session.revoke_failed";
            public const string AllSessionsRevoked = "session.revoked_all";
            public const string GuestCreated = "guest.created";
            public const string GuestTokenExpired = "guest.token_expired";
            public const string GuestClaimed = "guest.claimed";
            public const string GuestClaimRejected = "guest.claim_rejected";
            public const string GuestClaimFailed = "guest.claim_failed";
            public const string AdminRegistrationStarted = "admin.registration_started";
            public const string AdminSecretFailed = "admin.secret_failed";
            public const string AdminEmailVerified = "admin.email_verified";
            public const string PasswordResetRequested = "admin.password_reset_requested";
            public const string PasswordResetCompleted = "admin.password_reset_completed";
            public const string PasswordResetFailed = "admin.password_reset_failed";
            public const string PasswordChanged = "admin.password_changed";
            public const string PasswordChangeFailed = "admin.password_change_failed";
            public const string AccountDeletionRequested = "account.deletion_requested";
            public const string AccountDeletionCancelled = "account.deletion_cancelled";
            public const string AccountDeleted = "account.deleted";
            public const string AccountDeleteFailed = "account.delete_failed";
        }

        public static class Device
        {
            public const string Registered = "device.registered";
            public const string Updated = "device.updated";
            public const string Unregistered = "device.unregistered";
            public const string OwnershipConflict = "device.ownership_conflict";
            public const string LoggedOut = "device.logout_disabled";
            public const string LoginBound = "device.login_bound";
            public const string LoginBindFailed = "device.login_bind_failed";
            public const string AccountSwitched = "device.account_switched";
        }

        public static class Poll
        {
            public const string Created = "poll.created";
            public const string Updated = "poll.updated";
            public const string Published = "poll.published";
            public const string Closed = "poll.closed";
            public const string Deleted = "poll.deleted";
            public const string PermissionDenied = "poll.permission_denied";
            public const string OperationFailed = "poll.operation_failed";
            public const string VoteCast = "vote.cast";
            public const string VoteRejected = "vote.rejected";
        }

        public static class Notification
        {
            public const string OtpEmail = "otp_email";
            public const string AdminEmail = "admin_email";
            public const string Push = "push";
        }

        public static class Jobs
        {
            public const string GuestCleanup = "guest_cleanup";
            public const string AuditRetention = "audit_retention";
            public const string AccountDeletion = "account_deletion";
            public const string SessionCleanup = "session_cleanup";
            public const string VoteNotificationRetention = "vote_notification_retention";
        }
    }
}
