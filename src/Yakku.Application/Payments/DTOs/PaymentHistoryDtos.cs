namespace Yakku.Application.Payments.DTOs
{
    public sealed class PaymentHistoryItemResponse
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PurchaseType { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int AmountInPaise { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? RazorpayOrderId { get; set; }
        public string? ContentType { get; set; }
        public Guid? ContentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public sealed class PaymentOrderStatusResponse
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PurchaseType { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int AmountInPaise { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? RazorpayOrderId { get; set; }
        public string? ContentType { get; set; }
        public Guid? ContentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public IReadOnlyList<PaymentAttemptStatusItem> Attempts { get; set; } = [];
    }

    public sealed class PaymentAttemptStatusItem
    {
        public Guid AttemptId { get; set; }
        public string? RazorpayPaymentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RazorpayStatus { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CapturedAt { get; set; }
        public DateTime? FailedAt { get; set; }
    }
}
