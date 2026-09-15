using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Email
{
    public sealed class OtpEmailWorker : BackgroundService
    {
        private readonly ChannelReader<EmailJob> _reader;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<OtpEmailWorker> _logger;

        public OtpEmailWorker(
            Channel<EmailJob> channel,
            IServiceScopeFactory scopeFactory,
            IEmailSender emailSender,
            ILogger<OtpEmailWorker> logger)
        {
            _reader = channel.Reader;
            _scopeFactory = scopeFactory;
            _emailSender = emailSender;
            _logger = logger;
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
                        "Unhandled fault while processing OTP email job {EmailLogId}",
                        job.EmailLogId);
                    await WriteSystemLogAsync(
                        SystemLogLevel.Error,
                        SystemLogEventTypes.OtpEmailWorkerFault,
                        "OTP email worker fault.",
                        new { emailLogId = job.EmailLogId, challengeId = job.ChallengeId },
                        stoppingToken);
                }
            }
        }

        private async Task ProcessJobAsync(EmailJob job, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var emailLogs = scope.ServiceProvider.GetRequiredService<IEmailLogRepository>();
            var otpStore = scope.ServiceProvider.GetRequiredService<IOtpChallengeStore>();
            var systemLogs = scope.ServiceProvider.GetRequiredService<ISystemLogWriter>();

            var emailLog = await emailLogs.GetByIdAsync(job.EmailLogId, stoppingToken);
            if (emailLog is null)
            {
                _logger.LogWarning("EmailLog {EmailLogId} was not found for OTP job.", job.EmailLogId);
                return;
            }

            if (!await IsChallengeCurrentAsync(otpStore, job, stoppingToken))
            {
                emailLog.MarkCancelled("Superseded", "OTP challenge was replaced before send.");
                await emailLogs.SaveChangesAsync(stoppingToken);
                return;
            }

            emailLog.MarkSending();
            await emailLogs.SaveChangesAsync(stoppingToken);

            for (var attempt = 1; attempt <= emailLog.MaxAttempts; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (!await IsChallengeCurrentAsync(otpStore, job, stoppingToken))
                {
                    emailLog.MarkCancelled("Superseded", "OTP challenge was replaced during send.");
                    await emailLogs.SaveChangesAsync(stoppingToken);
                    return;
                }

                if (attempt > 1)
                {
                    var delayIndex = Math.Min(attempt - 2, OtpEmailQueueOptions.RetryDelays.Length - 1);
                    await Task.Delay(WithJitter(OtpEmailQueueOptions.RetryDelays[delayIndex]), stoppingToken);
                }

                emailLog.RecordAttempt();
                await emailLogs.SaveChangesAsync(stoppingToken);

                var result = await _emailSender.SendOtpAsync(job.RecipientEmail, job.Otp, stoppingToken);
                if (result.Succeeded)
                {
                    emailLog.MarkSent(result.ProviderMessageId);
                    await emailLogs.SaveChangesAsync(stoppingToken);
                    return;
                }

                if (result.IsPermanentFailure || attempt >= emailLog.MaxAttempts)
                {
                    emailLog.MarkFailed(
                        result.ErrorCode ?? "SendFailed",
                        result.ErrorMessage);
                    await emailLogs.SaveChangesAsync(stoppingToken);

                    await otpStore.DeleteIfChallengeMatchesAsync(
                        job.RecipientEmail,
                        job.ChallengeId,
                        stoppingToken);

                    await systemLogs.WriteAsync(
                        new SystemLogWriteRequest
                        {
                            Level = SystemLogLevel.Warning,
                            EventType = SystemLogEventTypes.OtpEmailPermanentlyFailed,
                            Message = "OTP email permanently failed.",
                            Details = new
                            {
                                emailLogId = emailLog.Id,
                                challengeId = job.ChallengeId,
                                errorCode = result.ErrorCode
                            },
                            UserId = emailLog.UserId
                        },
                        stoppingToken);
                    return;
                }
            }
        }

        private static async Task<bool> IsChallengeCurrentAsync(
            IOtpChallengeStore otpStore,
            EmailJob job,
            CancellationToken cancellationToken)
        {
            var challenge = await otpStore.GetAsync(job.RecipientEmail, cancellationToken);
            return challenge is not null && challenge.ChallengeId == job.ChallengeId;
        }

        private static TimeSpan WithJitter(TimeSpan delay)
        {
            var factor = 0.8 + (Random.Shared.NextDouble() * 0.4);
            return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * factor);
        }

        private async Task WriteSystemLogAsync(
            SystemLogLevel level,
            string eventType,
            string message,
            object details,
            CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var systemLogs = scope.ServiceProvider.GetRequiredService<ISystemLogWriter>();
                await systemLogs.WriteAsync(
                    new SystemLogWriteRequest
                    {
                        Level = level,
                        EventType = eventType,
                        Message = message,
                        Details = details
                    },
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to write system log {EventType}", eventType);
            }
        }
    }
}
