namespace Yakku.Application.Payments.DTOs
{
    public class VerifyPaymentRequest
    {
        public string? RazorpayOrderId { get; set; }
        public string? RazorpayPaymentId { get; set; }
        public string? RazorpaySignature { get; set; }
    }

    public class VerifyPaymentResponse
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool AlreadyFulfilled { get; set; }
        public string PurchaseType { get; set; } = string.Empty;
        public string? PlanCode { get; set; }
        public DateTime? PlanExpiresAt { get; set; }
        public string? ContentType { get; set; }
        public Guid? ContentId { get; set; }

        /// <summary>The conversation a top-up was granted to (null for other purchases).</summary>
        public Guid? ConversationId { get; set; }

        /// <summary>Questions a top-up added to the conversation (null when none was added).</summary>
        public int? TopUpCredits { get; set; }
        public string? RazorpayPaymentId { get; set; }
    }
}
