using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Devices.Interfaces;

namespace Yakku.Application.Auth.Services
{
    public class SessionCleanupService : ISessionCleanupService
    {
        private readonly IUserSessionRepository _sessions;
        private readonly IDeviceRepository _devices;
        private readonly IAdminSessionRepository? _adminSessions;

        public SessionCleanupService(
            IUserSessionRepository sessions,
            IDeviceRepository devices,
            IAdminSessionRepository? adminSessions = null)
        {
            _adminSessions = adminSessions;
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

            // Admin sessions that ended (revoked or past their absolute limit) are kept for the same audit window, then removed.
            if (_adminSessions is not null)
            {
                deleted += await _adminSessions.DeleteEndedBeforeAsync(
                    utcNow.AddDays(-OperationalSettings.Sessions.RetentionDays), cancellationToken);
            }

            return new SessionCleanupResult(detached, deleted);
        }
    }
}
