using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Sms;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Sms
{
    /// <summary>
    /// Delivers queued OTP SMS through MSG91. Retries are immediate (no delay) up to
    /// <see cref="OtpSmsQueueOptions.MaxAttempts"/>; permanent failures revoke the challenge.
    /// </summary>
    public sealed class OtpSmsWorker : BackgroundService
    {
        private readonly ChannelReader<SmsOtpJob> _reader;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISmsService _sms;
        private readonly ILogger<OtpSmsWorker> _logger;
        private readonly IAuditLogWriter _audit;

        public OtpSmsWorker(
            Channel<SmsOtpJob> channel,
            IServiceScopeFactory scopeFactory,
            ISmsService sms,
            ILogger<OtpSmsWorker> logger,
            IAuditLogWriter audit)
        {
            _reader = channel.Reader;
            _scopeFactory = scopeFactory;
            _sms = sms;
            _logger = logger;
            _audit = audit;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var job in _reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessJobAsync(job, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Unhandled fault while processing OTP SMS job {ChallengeId}",
                            job.ChallengeId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is stopping: leave quietly.
            }
        }

        /// <summary>Processes one job. Public so tests can drain the channel without starting the host.</summary>
        public async Task ProcessJobAsync(SmsOtpJob job, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var otpStore = scope.ServiceProvider.GetRequiredService<IOtpChallengeStore>();

            if (!await IsChallengeCurrentAsync(otpStore, job, stoppingToken))
            {
                _logger.LogInformation(
                    "Skipping OTP SMS for superseded challenge {ChallengeId}.",
                    job.ChallengeId);
                await AuditAttemptAsync(job, 1, NotificationAuditStatus.Skipped, null, "superseded", null, false);
                return;
            }

            for (var attempt = 1; attempt <= OtpSmsQueueOptions.MaxAttempts; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (!await IsChallengeCurrentAsync(otpStore, job, stoppingToken))
                {
                    _logger.LogInformation(
                        "Stopping OTP SMS for superseded challenge {ChallengeId}.",
                        job.ChallengeId);
                    await AuditAttemptAsync(job, attempt, NotificationAuditStatus.Skipped, null, "superseded", null, false);
                    return;
                }

                SmsSendResult result;
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    result = await _sms.SendOtpAsync(
                        new SmsOtpMessage(job.PhoneNumber, job.Otp, job.ChallengeId),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    stopwatch.Stop();
                    _logger.LogWarning(
                        exception,
                        "OTP SMS attempt {Attempt} threw for challenge {ChallengeId} ({ExceptionType}).",
                        attempt,
                        job.ChallengeId,
                        exception.GetType().Name);

                    var willRetryException = attempt < OtpSmsQueueOptions.MaxAttempts;
                    await AuditAttemptAsync(
                        job,
                        attempt,
                        NotificationAuditStatus.Failed,
                        null,
                        SmsFailureCategory.Network.ToString(),
                        (int)stopwatch.ElapsedMilliseconds,
                        willRetryException);

                    if (!willRetryException)
                    {
                        await RevokeUndeliveredAsync(otpStore, job, stoppingToken);
                        _logger.LogWarning(
                            "OTP SMS permanently failed for challenge {ChallengeId} after unexpected errors.",
                            job.ChallengeId);
                        return;
                    }

                    continue;
                }

                stopwatch.Stop();

                var permanent = IsPermanentFailure(result.Failure);
                var willRetry = !result.Accepted
                    && !permanent
                    && attempt < OtpSmsQueueOptions.MaxAttempts;

                await AuditAttemptAsync(
                    job,
                    attempt,
                    result.Accepted ? NotificationAuditStatus.Accepted : NotificationAuditStatus.Failed,
                    result.ProviderRequestId,
                    result.Accepted ? null : result.Failure.ToString(),
                    result.LatencyMs > 0 ? result.LatencyMs : (int)stopwatch.ElapsedMilliseconds,
                    willRetry);

                if (result.Accepted)
                {
                    return;
                }

                if (permanent || attempt >= OtpSmsQueueOptions.MaxAttempts)
                {
                    await RevokeUndeliveredAsync(otpStore, job, stoppingToken);
                    _logger.LogWarning(
                        "OTP SMS permanently failed for challenge {ChallengeId}. Failure: {Failure}",
                        job.ChallengeId,
                        result.Failure);
                    return;
                }

                // Immediate retry — no delay.
            }
        }

        private Task AuditAttemptAsync(
            SmsOtpJob job,
            int attempt,
            NotificationAuditStatus status,
            string? providerMessageId,
            string? errorCode,
            int? latencyMs,
            bool willRetry)
        {
            return _audit.WriteNotificationAsync(
                new NotificationAuditEntry(
                    NotificationChannel.Sms,
                    "msg91",
                    AuditEvents.Notification.OtpSms,
                    null,
                    null,
                    job.PhoneNumber,
                    job.ChallengeId,
                    attempt,
                    willRetry,
                    status,
                    providerMessageId,
                    errorCode,
                    null,
                    latencyMs),
                CancellationToken.None);
        }

        private static bool IsPermanentFailure(SmsFailureCategory failure)
        {
            return failure is SmsFailureCategory.InvalidRecipient
                or SmsFailureCategory.Authentication
                or SmsFailureCategory.Template
                or SmsFailureCategory.Rejected
                or SmsFailureCategory.InvalidResponse
                or SmsFailureCategory.Disabled;
        }

        private static async Task<bool> IsChallengeCurrentAsync(
            IOtpChallengeStore otpStore,
            SmsOtpJob job,
            CancellationToken cancellationToken)
        {
            var challenge = await otpStore.GetAsync(job.PhoneNumber, cancellationToken);
            return challenge is not null && challenge.ChallengeId == job.ChallengeId;
        }

        private async Task RevokeUndeliveredAsync(
            IOtpChallengeStore otpStore,
            SmsOtpJob job,
            CancellationToken cancellationToken)
        {
            try
            {
                var revoked = await otpStore.RevokeUndeliveredAsync(
                    job.PhoneNumber,
                    job.ChallengeId,
                    cancellationToken);
                _logger.LogInformation(
                    "Undelivered OTP SMS challenge {ChallengeId} {Outcome}.",
                    job.ChallengeId,
                    revoked ? "was invalidated" : "was already replaced or expired");
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                _logger.LogWarning(
                    "Could not invalidate undelivered OTP SMS challenge {ChallengeId} ({ExceptionType}); it expires on its own.",
                    job.ChallengeId,
                    exception.GetType().Name);
            }
        }
    }
}
