using Yakku.Domain.Entities;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentFulfillmentRepository
    {
        Task<PaymentOrder?> FindOrderForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default);

        Task<PaymentAttempt?> FindAttemptByRazorpayPaymentIdAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default);

        Task<PaymentFulfillmentResult> FulfillCapturedAsync(
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a failed gateway attempt without failing the whole order (another attempt may still succeed).
        /// No-ops when the payment was already captured.
        /// </summary>
        Task RecordFailedAttemptAsync(
            Guid paymentOrderId,
            string razorpayPaymentId,
            int amountInPaise,
            string currency,
            string? paymentMethod,
            string? razorpayStatus,
            string? failureCode,
            string? failureReason,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
    }
}
