using Yakku.Domain.Entities;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentRefundRepository
    {
        Task<PaymentAttempt?> FindAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);

        Task<PaymentOrder?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

        Task<PaymentRefund?> FindProcessedByAttemptAsync(
            Guid attemptId,
            CancellationToken cancellationToken = default);

        Task AddAsync(PaymentRefund refund, CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
