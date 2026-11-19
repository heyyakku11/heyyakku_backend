using Yakku.Application.Payments.DTOs;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentVerificationService
    {
        Task<VerifyPaymentResponse> VerifyAsync(
            Guid userId,
            VerifyPaymentRequest request,
            string? correlationId,
            CancellationToken cancellationToken = default);
    }
}
