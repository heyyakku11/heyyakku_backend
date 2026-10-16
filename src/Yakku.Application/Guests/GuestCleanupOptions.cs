using Yakku.Application.Common;

namespace Yakku.Application.Guests
{
    public sealed record GuestCleanupOptions(TimeSpan GracePeriod, int BatchSize)
    {
        public static readonly GuestCleanupOptions Default = new(
            TimeSpan.FromDays(OperationalSettings.Guests.CleanupGraceDays),
            OperationalSettings.Guests.CleanupBatchSize);
    }
}
