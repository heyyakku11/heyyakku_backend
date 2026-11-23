using Yakku.Domain.Enums;

namespace Yakku.Application.Payments
{
    /// <summary>
    /// Best-effort payment audit sink. Implementations must never throw into the caller and must never persist
    /// secrets, authorization headers or raw payment credentials.
    /// </summary>
    public interface IPaymentLogWriter
    {
        Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken = default);
    }

    public sealed record PaymentLogEntry(
        string EventType,
        string EventSource,
        PaymentLogStatus Status,
        string Message,
        Guid? PaymentOrderId = null,
        Guid? PaymentAttemptId = null,
        Guid? UserId = null,
        string? CorrelationId = null,
        string? RazorpayOrderId = null,
        string? RazorpayPaymentId = null,
        IReadOnlyDictionary<string, string?>? Metadata = null,
        int? LatencyMs = null);
}
