using Yakku.Application.Audit;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Guests.Services
{
    public class GuestIdentityService : IGuestIdentityService
    {
        private readonly IGuestRepository _guestRepository;
        private readonly IGuestTokenGenerator _tokenGenerator;
        private readonly IGuestTokenHasher _tokenHasher;
        private readonly IAuditLogWriter _audit;

        public GuestIdentityService(
            IGuestRepository guestRepository,
            IGuestTokenGenerator tokenGenerator,
            IGuestTokenHasher tokenHasher,
            IAuditLogWriter audit)
        {
            _guestRepository = guestRepository;
            _tokenGenerator = tokenGenerator;
            _tokenHasher = tokenHasher;
            _audit = audit;
        }

        public async Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                var tokenHash = _tokenHasher.Hash(rawToken.Trim());
                var existing = await _guestRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
                if (existing is not null)
                {
                    if (!existing.IsExpired(DateTime.UtcNow))
                    {
                        // A valid identity is always reused: no new row, no new token.
                        existing.Touch();
                        await _guestRepository.SaveChangesAsync(cancellationToken);
                        return ToResult(existing);
                    }

                    // An expired identity is never revived. It is left for the cleanup job and replaced below.
                    await _audit.WriteAuthenticationAsync(
                        new AuthenticationAuditEntry(
                            AuditActorType.Guest,
                            existing.Id,
                            AuditEvents.Auth.GuestTokenExpired,
                            AuditOutcome.Failure,
                            FailureReason: "guest_expired"),
                        CancellationToken.None);
                }
            }

            var token = _tokenGenerator.Generate();
            var guest = new Guest(_tokenHasher.Hash(token));
            await _guestRepository.AddAsync(guest, cancellationToken);
            await _guestRepository.SaveChangesAsync(cancellationToken);

            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Guest,
                    guest.Id,
                    AuditEvents.Auth.GuestCreated,
                    AuditOutcome.Success),
                CancellationToken.None);

            return ToResult(guest, token);
        }

        private static GuestEstablishResult ToResult(Guest guest, string? rawTokenToSet = null)
        {
            return new GuestEstablishResult
            {
                GuestId = guest.Id,
                ExpiresAt = guest.ExpiresAt,
                RawTokenToSet = rawTokenToSet
            };
        }
    }
}
