namespace Yakku.Application.Payments.DTOs
{
    public sealed class CreatePaymentRefundRequest
    {
        /// <summary>Optional partial amount in paise; defaults to the full captured attempt amount.</summary>
        public int? AmountInPaise { get; set; }

        public string? Reason { get; set; }
    }

    public sealed class PaymentRefundResponse
    {
        public Guid RefundId { get; set; }
        public Guid PaymentAttemptId { get; set; }
        public Guid PaymentOrderId { get; set; }
        public string? RazorpayRefundId { get; set; }
        public int AmountInPaise { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string AccessNote { get; set; } =
            "Refund does not automatically revoke plan entitlements or permanent card unlocks.";
    }
}
