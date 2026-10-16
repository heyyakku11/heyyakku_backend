using Yakku.Application.Common;

namespace Yakku.Infrastructure.Configuration
{
    public sealed record AuditRetentionOptions(
        int AuthenticationDays,
        int NotificationDays,
        int SystemDays,
        int PollDays)
    {
        public static readonly AuditRetentionOptions Default = new(
            OperationalSettings.Retention.AuthenticationDays,
            OperationalSettings.Retention.NotificationDays,
            OperationalSettings.Retention.SystemLogDays,
            OperationalSettings.Retention.PollDays);
    }
}
