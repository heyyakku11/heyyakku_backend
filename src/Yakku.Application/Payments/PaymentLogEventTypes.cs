namespace Yakku.Application.Payments
{
    /// <summary>Stable event type strings for <see cref="Domain.Entities.PaymentLog"/> rows.</summary>
    public static class PaymentLogEventTypes
    {
        public const string PaymentOrderCreationRequested = "PaymentOrderCreationRequested";
        public const string PaymentOrderCreated = "PaymentOrderCreated";
        public const string PaymentOrderCreationFailed = "PaymentOrderCreationFailed";
        public const string PaymentOrderExpired = "PaymentOrderExpired";
        public const string CheckoutInitiated = "CheckoutInitiated";
        public const string PaymentVerificationRequested = "PaymentVerificationRequested";
        public const string PaymentSignatureVerified = "PaymentSignatureVerified";
        public const string PaymentSignatureVerificationFailed = "PaymentSignatureVerificationFailed";
        public const string PaymentAuthorized = "PaymentAuthorized";
        public const string PaymentCaptured = "PaymentCaptured";
        public const string PaymentFailed = "PaymentFailed";
        public const string WebhookReceived = "WebhookReceived";
        public const string WebhookSignatureVerified = "WebhookSignatureVerified";
        public const string WebhookSignatureVerificationFailed = "WebhookSignatureVerificationFailed";
        public const string WebhookProcessingFailed = "WebhookProcessingFailed";
        public const string EntitlementCreated = "EntitlementCreated";
        public const string EntitlementSuperseded = "EntitlementSuperseded";
        public const string CardUnlockCreated = "CardUnlockCreated";
        public const string RefundRequested = "RefundRequested";
        public const string RefundProcessed = "RefundProcessed";
        public const string PaymentReconciliationMismatch = "PaymentReconciliationMismatch";
    }

    public static class PaymentLogSources
    {
        public const string Api = "Api";
        public const string Webhook = "Webhook";
        public const string Worker = "Worker";
        public const string System = "System";
        public const string Admin = "Admin";
    }
}
