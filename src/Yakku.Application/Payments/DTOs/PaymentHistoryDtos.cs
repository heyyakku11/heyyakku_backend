namespace Yakku.Application.Payments.DTOs
{
    public sealed class PaymentHistoryItemResponse
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int AmountInPaise { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? RazorpayOrderId { get; set; }
        public string? ContentType { get; set; }
        public Guid? ContentId { get; set; }
        public Guid? ConversationId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
