using System.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Storage;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Users.Services
{
    public sealed class AccountDeletionService : IAccountDeletionService
    {
        private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(OperationalSettings.AccountDeletion.GraceDays);

        private readonly IUserRepository _users;
        private readonly ISessionService _sessions;
        private readonly IAccountDeletionRepository _deletions;
        private readonly IObjectStorage _storage;
        private readonly IOtpChallengeStore _otpChallenges;
        private readonly IAuditLogWriter _audit;

        public AccountDeletionService(
            IUserRepository users,
            ISessionService sessions,
            IAccountDeletionRepository deletions,
            IObjectStorage storage,
            IOtpChallengeStore otpChallenges,
            IAuditLogWriter audit)
        {
            _users = users;
            _sessions = sessions;
            _deletions = deletions;
            _storage = storage;
            _otpChallenges = otpChallenges;
            _audit = audit;
        }

        public async Task<AccountDeletionResponse> RequestDeletionAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _users.GetByIdAsync(userId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "User not found.");

            if (!user.IsPendingDeletion)
            {
                user.RequestDeletion(DateTime.UtcNow, GracePeriod);
                await _users.SaveChangesAsync(cancellationToken);

                await _audit.WriteAuthenticationAsync(
                    new AuthenticationAuditEntry(
                        AuditActorType.User,
                        user.Id,
                        AuditEvents.Auth.AccountDeletionRequested,
                        AuditOutcome.Success,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["scheduledFor"] = user.DeletionScheduledFor!.Value.ToString("O")
                        }),
                    CancellationToken.None);
            }

            // Signed out everywhere; only a fresh OTP login can cancel the deletion. Re-running this on a repeated
            // request also revokes any session that was created in the meantime.
            await _sessions.RevokeAllAsync(user.Id, cancellationToken);

            return new AccountDeletionResponse
            {
                RequestedAt = user.DeletionRequestedAt!.Value,
                ScheduledFor = user.DeletionScheduledFor!.Value
            };
        }

        public async Task<AccountPurgeSummary> PurgeDueAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var ids = await _deletions.ListDueUserIdsAsync(
                DateTime.UtcNow,
                OperationalSettings.AccountDeletion.BatchSize,
                cancellationToken);

            var deleted = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var id in ids)
            {
                try
                {
                    var result = await _deletions.PurgeAsync(id, DateTime.UtcNow, cancellationToken);
                    if (!result.Deleted)
                    {
                        skipped++;
                        continue;
                    }

                    deleted++;
                    await CleanUpAfterPurgeAsync(id, result, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                    await _audit.WriteAuthenticationAsync(
                        new AuthenticationAuditEntry(
                            AuditActorType.System,
                            id,
                            AuditEvents.Auth.AccountDeleteFailed,
                            AuditOutcome.Failure,
                            FailureReason: exception.GetType().Name),
                        CancellationToken.None);
                }
            }

            stopwatch.Stop();
            if (ids.Count > 0)
            {
                await _audit.WriteSystemAsync(
                    new SystemLogEntry(
                        SystemLogCategory.BackgroundJob,
                        failed > 0 ? SystemLogSeverity.Warning : SystemLogSeverity.Information,
                        nameof(AccountDeletionService),
                        failed > 0 ? "Account deletion finished with failures." : "Account deletion finished.",
                        DurationMs: (int)stopwatch.Elapsed.TotalMilliseconds,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["job"] = AuditEvents.Jobs.AccountDeletion,
                            ["examined"] = ids.Count.ToString(),
                            ["deleted"] = deleted.ToString(),
                            ["skipped"] = skipped.ToString(),
                            ["failed"] = failed.ToString()
                        }),
                    CancellationToken.None);
            }

            return new AccountPurgeSummary(ids.Count, deleted, skipped, failed);
        }

        /// <summary>
        /// Best effort after the database commit: the account is already gone, so a storage or Redis hiccup must not
        /// fail the run. Orphaned objects are logged and left for manual cleanup.
        /// </summary>
        private async Task CleanUpAfterPurgeAsync(
            Guid userId,
            AccountPurgeResult result,
            CancellationToken cancellationToken)
        {
            var objectFailures = 0;
            foreach (var item in result.Objects.Distinct())
            {
                try
                {
                    await _storage.DeleteAsync(item.BucketName, item.ObjectKey, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    objectFailures++;
                }
            }

            if (result.PhoneNumber is not null)
            {
                try
                {
                    await _otpChallenges.DeleteAsync(result.PhoneNumber, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The challenge expires on its own.
                }
            }

            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.System,
                    userId,
                    AuditEvents.Auth.AccountDeleted,
                    objectFailures > 0 ? AuditOutcome.Failure : AuditOutcome.Success,
                    FailureReason: objectFailures > 0 ? "storage_delete_failed" : null,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["objects"] = result.Objects.Count.ToString(),
                        ["objectFailures"] = objectFailures.ToString()
                    }),
                CancellationToken.None);
        }
    }
}
