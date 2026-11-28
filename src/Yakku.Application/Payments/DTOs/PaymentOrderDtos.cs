namespace Yakku.Application.Payments.DTOs
{
    /// <summary>
    /// Creates a checkout order. For <c>yakku_lite</c> / <c>yakku_love</c> send <see cref="PlanCode"/> only.
    /// For <c>this_card</c> also send <see cref="ContentType"/> and <see cref="ContentId"/>.
    /// For <c>top_up_19</c> / <c>top_up_29</c> send <see cref="ConversationId"/> (the asker's conversation).
    /// </summary>
    public class CreatePaymentOrderRequest
    {
        /// <summary>Plan code (<c>yakku_lite</c>, <c>yakku_love</c>, <c>this_card</c>, <c>top_up_19</c> or <c>top_up_29</c>).</summary>
        public string PlanCode { get; set; } = string.Empty;

        /// <summary><c>poll</c> or <c>check</c> when buying permanent access to one card.</summary>
        public string? ContentType { get; set; }

        public Guid? ContentId { get; set; }

        /// <summary>The ask back conversation a top-up adds questions to. Required for top-ups only.</summary>
        public Guid? ConversationId { get; set; }

        /// <summary>Client-generated unique key; retries with the same key reuse the same order.</summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>Checkout payload for Razorpay Checkout. Never includes the key secret.</summary>
    public class CreatePaymentOrderResponse
    {
        public Guid OrderId { get; set; }
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayKeyId { get; set; } = string.Empty;
        public int AmountInPaise { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string PurchaseType { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string? ContentType { get; set; }
        public Guid? ContentId { get; set; }
        public Guid? ConversationId { get; set; }
    }
}
