using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Email;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Email
{
    public sealed class OtpEmailWorker : BackgroundService
    {
        private readonly ChannelReader<EmailJob> _reader;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<OtpEmailWorker> _logger;
        private readonly IAuditLogWriter _audit;

        public OtpEmailWorker(
            Channel<EmailJob> channel,
            IServiceScopeFactory scopeFactory,
            IEmailSender emailSender,
            ILogger<OtpEmailWorker> logger,
            IAuditLogWriter audit)
        {
            _reader = channel.Reader;
            _scopeFactory = scopeFactory;
            _emailSender = emailSender;
            _logger = logger;
            _audit = audit;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                        "Unhandled fault while processing OTP email job {ChallengeId}",
                        job.ChallengeId);
                }
            }
        }

        private async Task ProcessJobAsync(EmailJob job, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var otpStore = scope.ServiceProvider.GetRequiredService<IOtpChallengeStore>();
            var adminOtpStore = scope.ServiceProvider.GetRequiredService<IAdminOtpChallengeStore>();

            if (!await IsChallengeCurrentAsync(otpStore, adminOtpStore, job, stoppingToken))
            {
                _logger.LogInformation(
                    "Skipping OTP email for superseded challenge {ChallengeId}.",
                    job.ChallengeId);
                await AuditAttemptAsync(job, 1, NotificationAuditStatus.Skipped, null, "superseded", null, null, false);
                return;
            }

            for (var attempt = 1; attempt <= OtpEmailQueueOptions.MaxAttempts; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (!await IsChallengeCurrentAsync(otpStore, adminOtpStore, job, stoppingToken))
                {
                    _logger.LogInformation(
                        "Stopping OTP email for superseded challenge {ChallengeId}.",
                        job.ChallengeId);
                    await AuditAttemptAsync(job, attempt, NotificationAuditStatus.Skipped, null, "superseded", null, null, false);
                    return;
                }

                if (attempt > 1)
                {
                    var delayIndex = Math.Min(attempt - 2, OtpEmailQueueOptions.RetryDelays.Length - 1);
                    await Task.Delay(WithJitter(OtpEmailQueueOptions.RetryDelays[delayIndex]), stoppingToken);
                }

                var stopwatch = Stopwatch.StartNew();
                var result = await _emailSender.SendOtpAsync(job.RecipientEmail, job.Otp, stoppingToken);
                stopwatch.Stop();

                // Resend returning an id means it accepted the message, not that the inbox received it.
                var willRetry = !result.Succeeded
                    && !result.IsPermanentFailure
                    && attempt < OtpEmailQueueOptions.MaxAttempts;
                await AuditAttemptAsync(
                    job,
                    attempt,
                    result.Succeeded ? NotificationAuditStatus.Accepted : NotificationAuditStatus.Failed,
                    result.ProviderMessageId,
                    result.ErrorCode,
                    result.ErrorMessage,
                    (int)stopwatch.ElapsedMilliseconds,
                    willRetry);

                if (result.Succeeded)
                {
                    return;
                }

                if (result.IsPermanentFailure || attempt >= OtpEmailQueueOptions.MaxAttempts)
                {
                    await DeleteMatchingChallengeAsync(otpStore, adminOtpStore, job, stoppingToken);
                    _logger.LogWarning(
                        "OTP email permanently failed for challenge {ChallengeId}. Error code: {ErrorCode}",
                        job.ChallengeId,
                        result.ErrorCode);
                    return;
                }
            }
        }

        private Task AuditAttemptAsync(
            EmailJob job,
            int attempt,
            NotificationAuditStatus status,
            string? providerMessageId,
            string? errorCode,
            string? errorMessage,
            int? latencyMs,
            bool willRetry)
        {
            return _audit.WriteNotificationAsync(
                new NotificationAuditEntry(
                    NotificationChannel.Email,
                    "resend",
                    AuditEvents.Notification.OtpEmail,
                    null,
                    null,
                    job.RecipientEmail,
                    job.ChallengeId,
                    attempt,
                    willRetry,
                    status,
                    providerMessageId,
                    errorCode,
                    errorMessage,
                    latencyMs),
                CancellationToken.None);
        }

        private static async Task<bool> IsChallengeCurrentAsync(
            IOtpChallengeStore otpStore,
            IAdminOtpChallengeStore adminOtpStore,
            EmailJob job,
            CancellationToken cancellationToken)
        {
            var userChallenge = await otpStore.GetAsync(job.RecipientEmail, cancellationToken);
            if (userChallenge is not null && userChallenge.ChallengeId == job.ChallengeId)
            {
                return true;
            }

            var adminChallenge = await adminOtpStore.GetAsync(job.RecipientEmail, cancellationToken);
            return adminChallenge is not null && adminChallenge.ChallengeId == job.ChallengeId;
        }

        private static async Task DeleteMatchingChallengeAsync(
            IOtpChallengeStore otpStore,
            IAdminOtpChallengeStore adminOtpStore,
            EmailJob job,
            CancellationToken cancellationToken)
        {
            await otpStore.DeleteIfChallengeMatchesAsync(job.RecipientEmail, job.ChallengeId, cancellationToken);

            var adminChallenge = await adminOtpStore.GetAsync(job.RecipientEmail, cancellationToken);
            if (adminChallenge is not null && adminChallenge.ChallengeId == job.ChallengeId)
            {
                await adminOtpStore.DeleteAsync(job.RecipientEmail, cancellationToken);
            }
        }

        private static TimeSpan WithJitter(TimeSpan delay)
        {
            var factor = 0.8 + (Random.Shared.NextDouble() * 0.4);
            return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * factor);
        }
    }
}
