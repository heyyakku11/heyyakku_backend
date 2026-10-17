using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<GuestIdentityService>? _logger;

        public GuestIdentityService(
            IGuestRepository guestRepository,
            IGuestTokenGenerator tokenGenerator,
            IGuestTokenHasher tokenHasher,
            IAuditLogWriter audit,
            ILogger<GuestIdentityService>? logger = null)
        {
            _guestRepository = guestRepository;
            _tokenGenerator = tokenGenerator;
            _tokenHasher = tokenHasher;
            _audit = audit;
            _logger = logger;
        }

        public async Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew(); //remove later
            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                var tokenHash = _tokenHasher.Hash(rawToken.Trim());
                LogStep("hash existing token", stopwatch);
                var existing = await _guestRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
                LogStep("db lookup by token hash", stopwatch);
                if (existing is not null)
                {
                    if (!existing.IsExpired(DateTime.UtcNow))
                    {
                        // A valid identity is always reused: no new row, no new token.
                        existing.Touch();
                        await _guestRepository.SaveChangesAsync(cancellationToken);
                        LogStep("db save (touch existing guest)", stopwatch);
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
                    LogStep("audit write (guest expired)", stopwatch);
                }
            }

            var token = _tokenGenerator.Generate();
            var guest = new Guest(_tokenHasher.Hash(token));
            LogStep("generate + hash new token", stopwatch);
            await _guestRepository.AddAsync(guest, cancellationToken);
            LogStep("db add (tracked in memory)", stopwatch);
            await _guestRepository.SaveChangesAsync(cancellationToken);
            LogStep("db save (insert new guest)", stopwatch);

            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Guest,
                    guest.Id,
                    AuditEvents.Auth.GuestCreated,
                    AuditOutcome.Success),
                CancellationToken.None);
            LogStep("audit write (guest created)", stopwatch);

            return ToResult(guest, token);
        }

        // Logs the time since the previous step and restarts the stopwatch.
        private void LogStep(string step, Stopwatch stopwatch)  //remove later
        {
            _logger?.LogInformation("guest timing | {Step} took {ElapsedMs} ms", step, stopwatch.Elapsed.TotalMilliseconds);
            stopwatch.Restart();
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
