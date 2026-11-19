using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Payments;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Payments
{
    /// <summary>
    /// Writes payment audit rows on a dedicated DbContext so they commit independently of the request transaction
    /// and never throw into the caller. Secrets must never be placed in <see cref="PaymentLogEntry.Metadata"/>.
    /// </summary>
    public sealed class PaymentLogWriter : IPaymentLogWriter
    {
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(3);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymentLogWriter> _logger;

        public PaymentLogWriter(
            IServiceScopeFactory scopeFactory,
            TimeProvider time,
            ILogger<PaymentLogWriter> logger)
        {
            _scopeFactory = scopeFactory;
            _time = time;
            _logger = logger;
        }

        public async Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken = default)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(WriteTimeout);

                var log = PaymentLog.Create(
                    AuditSanitizer.Truncate(entry.EventType, 64)!,
                    AuditSanitizer.Truncate(entry.EventSource, 32)!,
                    entry.Status,
                    AuditSanitizer.SanitizeText(entry.Message, 500) ?? "(no message)",
                    _time.GetUtcNow().UtcDateTime,
                    entry.PaymentOrderId,
                    entry.PaymentAttemptId,
                    entry.UserId,
                    AuditSanitizer.Truncate(entry.CorrelationId, 64),
                    AuditSanitizer.Truncate(entry.RazorpayOrderId, 64),
                    AuditSanitizer.Truncate(entry.RazorpayPaymentId, 64),
                    AuditSanitizer.SerializeMetadata(entry.Metadata));

                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<YakkuDbContext>();
                context.PaymentLogs.Add(log);
                await context.SaveChangesAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Failed to write payment audit log ({ExceptionType}).",
                    exception.GetType().Name);
            }
        }
    }
}
