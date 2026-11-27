using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.DTOs;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentQueryService
    {
        Task<(IReadOnlyList<PaymentHistoryItemResponse> Items, PaginationMeta Meta)> GetHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
