using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Guests.Services
{
    public class GuestIdentityService : IGuestIdentityService
    {
        private readonly IGuestRepository _guestRepository;
        private readonly IGuestSessionTokenFactory _sessionTokenFactory;
        private readonly IGuestTokenHasher _tokenHasher;
        private readonly IAuditLogWriter _audit;
        private readonly ILogger<GuestIdentityService>? _logger;

        public GuestIdentityService(
            IGuestRepository guestRepository,
            IGuestSessionTokenFactory sessionTokenFactory,
            IGuestTokenHasher tokenHasher,
            IAuditLogWriter audit,
            ILogger<GuestIdentityService>? logger = null)
        {
            _guestRepository = guestRepository;
            _sessionTokenFactory = sessionTokenFactory;
            _tokenHasher = tokenHasher;
            _audit = audit;
            _logger = logger;
        }

        public async Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            string? idempotencyKey = null,
            GuestClientInfo? client = null,
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

            var key = NormalizeIdempotencyKey(idempotencyKey);
            if (key is null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "idempotencyKey is required to create a guest identity.",
                    "idempotencyKey");
            }

            var byKey = await _guestRepository.FindByIdempotencyKeyAsync(key, cancellationToken);
            LogStep("db lookup by idempotency key", stopwatch);
            if (byKey is not null)
            {
                if (!byKey.IsExpired(DateTime.UtcNow))
                {
                    byKey.Touch();
                    await _guestRepository.SaveChangesAsync(cancellationToken);
                    LogStep("db save (touch idempotent guest)", stopwatch);
                    return ToResult(byKey, _sessionTokenFactory.FromIdempotencyKey(key));
                }

                // Free the unique index so the same key can mint a replacement. Never revive the expired row.
                byKey.ClearIdempotencyKey();
                await _guestRepository.SaveChangesAsync(cancellationToken);
                LogStep("db save (clear expired idempotency key)", stopwatch);
            }

            var token = _sessionTokenFactory.FromIdempotencyKey(key);
            var guest = new Guest(_tokenHasher.Hash(token), key);
            // Client details are recorded only when a guest is created; a reused guest keeps what it was created with.
            if (client is not null)
            {
                guest.CaptureClient(
                    IPAddress.TryParse(client.IpAddress, out var ip) ? ip : null,
                    client.UserAgent,
                    client.BrowserName,
                    client.BrowserVersion,
                    client.OsName,
                    client.DeviceType,
                    client.Language,
                    client.CountryCode,
                    client.CookiesEnabled);
            }
            LogStep("generate + hash new token", stopwatch);

            var saved = await _guestRepository.TryAddAsync(guest, cancellationToken);
            LogStep("db try-add (insert new guest)", stopwatch);
            if (saved is null)
            {
                // Concurrent duplicate idempotency key: return the winner.
                var raced = await _guestRepository.FindByIdempotencyKeyAsync(key, cancellationToken)
                    ?? throw new AppException(
                        409,
                        ApiErrorCodes.Conflict,
                        "A guest with this idempotency key already exists.",
                        "idempotencyKey");

                if (raced.IsExpired(DateTime.UtcNow))
                {
                    throw new AppException(
                        409,
                        ApiErrorCodes.Conflict,
                        "A guest with this idempotency key already exists.",
                        "idempotencyKey");
                }

                raced.Touch();
                await _guestRepository.SaveChangesAsync(cancellationToken);
                return ToResult(raced, token);
            }

            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Guest,
                    saved.Id,
                    AuditEvents.Auth.GuestCreated,
                    AuditOutcome.Success),
                CancellationToken.None);
            LogStep("audit write (guest created)", stopwatch);

            return ToResult(saved, token);
        }

        private static string? NormalizeIdempotencyKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
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
