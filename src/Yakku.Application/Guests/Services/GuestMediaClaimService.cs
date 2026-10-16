using Yakku.Application.Audit;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Guests.Services
{
    public sealed class GuestMediaClaimService : IGuestMediaClaimService
    {
        private readonly IGuestRepository _guests;
        private readonly IGuestTokenHasher _tokenHasher;
        private readonly IMediaUploadSessionRepository _sessions;
        private readonly IAuditLogWriter _audit;

        public GuestMediaClaimService(
            IGuestRepository guests,
            IGuestTokenHasher tokenHasher,
            IMediaUploadSessionRepository sessions,
            IAuditLogWriter audit)
        {
            _guests = guests;
            _tokenHasher = tokenHasher;
            _sessions = sessions;
            _audit = audit;
        }

        public async Task<GuestClaimResult> ClaimAndDeleteGuestAsync(
            string? rawGuestToken,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawGuestToken))
            {
                return GuestClaimResult.NoToken;
            }

            var tokenHash = _tokenHasher.Hash(rawGuestToken.Trim());
            var result = GuestClaimResult.NotFound;
            Guid? guestId = null;
            var transferred = 0;

            await _guests.ExecuteInTransactionAsync(async transactionToken =>
            {
                // The row lock serialises concurrent claims: the loser blocks, then finds no guest.
                var guest = await _guests.GetByTokenHashForUpdateAsync(tokenHash, transactionToken);
                if (guest is null)
                {
                    result = GuestClaimResult.NotFound;
                    return;
                }

                guestId = guest.Id;
                if (guest.IsExpired(DateTime.UtcNow))
                {
                    result = GuestClaimResult.Expired;
                    return;
                }

                transferred = await _sessions.ClaimGuestSessionsAsync(guest.Id, userId, transactionToken);
                _guests.Delete(guest);
                await _guests.SaveChangesAsync(transactionToken);
                result = GuestClaimResult.Claimed;
            }, cancellationToken);

            await AuditOutcomeAsync(result, userId, guestId, transferred);
            return result;
        }

        public async Task<GuestClaimResult> TryClaimAsync(
            string? rawGuestToken,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ClaimAndDeleteGuestAsync(rawGuestToken, userId, cancellationToken);
            }
            catch (Exception exception)
            {
                // Only the exception type is recorded: messages can embed SQL parameters.
                await _audit.WriteAuthenticationAsync(
                    new AuthenticationAuditEntry(
                        AuditActorType.User,
                        userId,
                        AuditEvents.Auth.GuestClaimFailed,
                        AuditOutcome.Failure,
                        FailureReason: exception.GetType().Name),
                    CancellationToken.None);
                return GuestClaimResult.Failed;
            }
        }

        private Task AuditOutcomeAsync(GuestClaimResult result, Guid userId, Guid? guestId, int transferred)
        {
            if (result == GuestClaimResult.NoToken || result == GuestClaimResult.NotFound)
            {
                return Task.CompletedTask;
            }

            var metadata = new Dictionary<string, string?>
            {
                ["guestId"] = guestId?.ToString(),
                ["sessionsTransferred"] = transferred.ToString()
            };

            var entry = result == GuestClaimResult.Claimed
                ? new AuthenticationAuditEntry(
                    AuditActorType.User,
                    userId,
                    AuditEvents.Auth.GuestClaimed,
                    AuditOutcome.Success,
                    Metadata: metadata)
                : new AuthenticationAuditEntry(
                    AuditActorType.User,
                    userId,
                    AuditEvents.Auth.GuestClaimRejected,
                    AuditOutcome.Failure,
                    FailureReason: "guest_expired",
                    Metadata: metadata);

            return _audit.WriteAuthenticationAsync(entry, CancellationToken.None);
        }
    }
}
