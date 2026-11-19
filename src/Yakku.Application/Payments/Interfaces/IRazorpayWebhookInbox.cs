using Yakku.Domain.Entities;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IRazorpayWebhookInbox
    {
        /// <summary>
        /// Persists a verified webhook. Returns the stored row, or the existing row when the Razorpay event id
        /// was already received (dedupe).
        /// </summary>
        Task<(WebhookEvent Event, bool Created)> TryEnqueueAsync(
            string razorpayEventId,
            string eventType,
            string rawPayload,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task<WebhookEvent?> ClaimNextAsync(int maxRetries, CancellationToken cancellationToken = default);

        Task<WebhookPayload?> GetPayloadAsync(Guid eventId, CancellationToken cancellationToken = default);

        Task MarkProcessedAsync(Guid eventId, DateTime utcNow, CancellationToken cancellationToken = default);

        Task MarkFailedAsync(Guid eventId, string? errorMessage, CancellationToken cancellationToken = default);

        Task ReclaimStaleProcessingAsync(
            TimeSpan staleAfter,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
    }

    public sealed record WebhookPayload(Guid Id, string EventType, string Payload);
}
