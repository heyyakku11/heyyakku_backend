using Yakku.Domain.Entities;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentQueryRepository
    {
        Task<(IReadOnlyList<PaymentOrder> Orders, int TotalCount)> ListOrdersForUserAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<string?> ResolvePlanNameAsync(Guid? planId, CancellationToken cancellationToken = default);
    }
}
