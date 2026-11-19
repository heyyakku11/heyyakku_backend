using Yakku.Application.Payments.DTOs;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentRefundService
    {
        Task<PaymentRefundResponse> CreateRefundAsync(
            Guid paymentAttemptId,
            CreatePaymentRefundRequest request,
            string? correlationId,
            CancellationToken cancellationToken = default);
    }
}
