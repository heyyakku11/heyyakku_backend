namespace Yakku.Application.Payments.Interfaces
{
    /// <summary>Razorpay Orders/Payments API. Never retries order creation without an idempotency receipt.</summary>
    public interface IRazorpayClient
    {
        bool IsEnabled { get; }

        Task<RazorpayOrderResult> CreateOrderAsync(
            RazorpayCreateOrderRequest request,
            CancellationToken cancellationToken = default);

        Task<RazorpayPaymentResult> GetPaymentAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default);

        /// <summary>Lists payments attached to a Razorpay order (used by reconciliation).</summary>
        Task<IReadOnlyList<RazorpayPaymentResult>> GetOrderPaymentsAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default);

        Task<RazorpayRefundResult> CreateRefundAsync(
            RazorpayCreateRefundRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record RazorpayCreateOrderRequest(
        int AmountInPaise,
        string Currency,
        string Receipt,
        bool PaymentCapture,
        string? NotesCorrelationId = null);

    public sealed record RazorpayOrderResult(
        bool Succeeded,
        string? RazorpayOrderId,
        int? AmountInPaise,
        string? Currency,
        string? Status,
        string? ErrorCode,
        string? ErrorMessage);

    public sealed record RazorpayPaymentResult(
        bool Succeeded,
        string? RazorpayPaymentId,
        string? RazorpayOrderId,
        int? AmountInPaise,
        string? Currency,
        string? Status,
        bool Captured,
        string? Method,
        string? ErrorCode,
        string? ErrorMessage);

    public sealed record RazorpayCreateRefundRequest(
        string RazorpayPaymentId,
        int AmountInPaise,
        string? NotesReason = null);

    public sealed record RazorpayRefundResult(
        bool Succeeded,
        string? RazorpayRefundId,
        string? Status,
        int? AmountInPaise,
        string? ErrorCode,
        string? ErrorMessage);
}
