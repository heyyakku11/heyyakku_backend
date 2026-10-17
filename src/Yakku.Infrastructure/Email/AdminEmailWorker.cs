using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Email;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Email
{
    /// <summary>Sends the admin registration, forgot-password and change-password emails with retries.</summary>
    public sealed class AdminEmailWorker : BackgroundService
    {
        private readonly ChannelReader<AdminEmailJob> _reader;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AdminEmailWorker> _logger;
        private readonly IAuditLogWriter _audit;

        public AdminEmailWorker(
            Channel<AdminEmailJob> channel,
            IEmailSender emailSender,
            ILogger<AdminEmailWorker> logger,
            IAuditLogWriter audit)
        {
            _reader = channel.Reader;
            _emailSender = emailSender;
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
                            "Unhandled fault while processing admin email job {CorrelationId}",
                            job.CorrelationId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is stopping: leave quietly.
            }
        }

        private async Task ProcessJobAsync(AdminEmailJob job, CancellationToken stoppingToken)
        {
            var templateId = AdminEmailTemplateCatalog.TemplateId(job.Template);
            if (templateId is null)
            {
                _logger.LogWarning(
                    "Admin email {Template} not sent: {EnvName} is not configured.",
                    job.Template,
                    AdminEmailTemplateCatalog.EnvName(job.Template));
                await AuditAttemptAsync(job, 1, NotificationAuditStatus.Failed, null, "TemplateNotConfigured", null, null, false);
                return;
            }

            var subject = job.Variables.TryGetValue(AdminEmailVariables.Heading, out var heading)
                ? heading
                : "Yakku admin";

            for (var attempt = 1; attempt <= OtpEmailQueueOptions.MaxAttempts; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (attempt > 1)
                {
                    var delayIndex = Math.Min(attempt - 2, OtpEmailQueueOptions.RetryDelays.Length - 1);
                    await Task.Delay(WithJitter(OtpEmailQueueOptions.RetryDelays[delayIndex]), stoppingToken);
                }

                var stopwatch = Stopwatch.StartNew();
                var result = await _emailSender.SendTemplateAsync(
                    job.RecipientEmail,
                    templateId,
                    subject,
                    job.Variables,
                    stoppingToken);
                stopwatch.Stop();

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
                    _logger.LogWarning(
                        "Admin email {Template} permanently failed. Error code: {ErrorCode}",
                        job.Template,
                        result.ErrorCode);
                    return;
                }
            }
        }

        private Task AuditAttemptAsync(
            AdminEmailJob job,
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
                    AuditEvents.Notification.AdminEmail,
                    null,
                    null,
                    job.RecipientEmail,
                    job.CorrelationId,
                    attempt,
                    willRetry,
                    status,
                    providerMessageId,
                    errorCode,
                    errorMessage,
                    latencyMs),
                CancellationToken.None);
        }

        private static TimeSpan WithJitter(TimeSpan delay)
        {
            var factor = 0.8 + (Random.Shared.NextDouble() * 0.4);
            return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * factor);
        }
    }
}
