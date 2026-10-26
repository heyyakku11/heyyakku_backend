using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Devices.Interfaces;

namespace Yakku.Application.Auth.Services
{
    public class SessionCleanupService : ISessionCleanupService
    {
        private readonly IUserSessionRepository _sessions;
        private readonly IDeviceRepository _devices;

        public SessionCleanupService(IUserSessionRepository sessions, IDeviceRepository devices)
        {
            _sessions = sessions;
            _devices = devices;
        }

        public async Task<SessionCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            var detached = 0;
            var deleted = 0;

            await _devices.ExecuteInTransactionAsync(async transactionToken =>
            {
                // Detach first: deleting a session would otherwise null the device's user through the foreign key
                // without clearing its token.
                detached = await _devices.DetachWithEndedSessionsAsync(utcNow, transactionToken);
                deleted = await _sessions.DeleteEndedBeforeAsync(
                    utcNow.AddDays(-OperationalSettings.Sessions.RetentionDays), transactionToken);
            }, cancellationToken);

            return new SessionCleanupResult(detached, deleted);
        }
    }
}
