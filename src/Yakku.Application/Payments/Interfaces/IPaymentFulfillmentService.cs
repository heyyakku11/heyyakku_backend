using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Interfaces
{
    /// <summary>
    /// Single idempotent writer used by Checkout verify and (later) webhooks. Only a captured payment grants access.
    /// </summary>
    public interface IPaymentFulfillmentService
    {
        Task<PaymentFulfillmentResult> FulfillCapturedPaymentAsync(
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken = default);
    }

    public sealed record CapturedPaymentFulfillmentCommand(
        Guid PaymentOrderId,
        string RazorpayOrderId,
        string RazorpayPaymentId,
        int AmountInPaise,
        string Currency,
        string? PaymentMethod,
        string? RazorpayStatus,
        DateTime CapturedAtUtc,
        string? CorrelationId);

    /// <summary>
    /// What fulfillment did. <see cref="TopUpCredits"/> is set only when a conversation top-up was granted in this call
    /// (or was granted before, on a retry); null means it was not applied, and the order is then for review.
    /// </summary>
    public sealed record PaymentFulfillmentResult(
        bool AlreadyFulfilled,
        Guid PaymentOrderId,
        Guid PaymentAttemptId,
        PaymentPurchaseType PurchaseType,
        string? PlanCode,
        DateTime? PlanExpiresAt,
        ShareContentType? ContentType,
        Guid? ContentId,
        Guid UserId = default,
        Guid? ConversationId = null,
        int? TopUpCredits = null,
        string? ConversationStatus = null);
}
