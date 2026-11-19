using Yakku.Domain.Entities;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentReconciliationRepository
    {
        Task<IReadOnlyList<PaymentOrder>> ListExpiredOpenOrdersAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken = default);

        Task MarkOrderExpiredAsync(Guid orderId, DateTime utcNow, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PaymentOrder>> ListPendingOrdersWithGatewayIdAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PaymentOrder>> ListPaidOrdersMissingFulfillmentAsync(
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<WebhookEvent>> ListExhaustedFailedWebhooksAsync(
            int maxRetries,
            int take,
            CancellationToken cancellationToken = default);

        Task<PaymentAttempt?> FindCapturedAttemptAsync(
            Guid orderId,
            CancellationToken cancellationToken = default);
    }
}
