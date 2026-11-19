namespace Yakku.Application.Payments.Interfaces
{
    public interface IRazorpayWebhookProcessor
    {
        /// <summary>Processes one claimed inbox row. Safe to retry; fulfillment is idempotent.</summary>
        Task ProcessAsync(Guid webhookEventId, CancellationToken cancellationToken = default);
    }

    public interface IRazorpayWebhookReceiver
    {
        /// <summary>
        /// Verifies the webhook signature against the raw body, enqueues the event, and returns whether it was new.
        /// </summary>
        Task<RazorpayWebhookReceiveResult> ReceiveAsync(
            byte[] rawBody,
            string? signatureHeader,
            string? eventIdHeader,
            string? correlationId,
            CancellationToken cancellationToken = default);
    }

    public sealed record RazorpayWebhookReceiveResult(
        bool Accepted,
        bool Duplicate,
        string? ErrorCode,
        string? ErrorMessage,
        Guid? WebhookEventId);
}
