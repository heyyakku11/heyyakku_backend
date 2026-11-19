using Yakku.Application.Payments.DTOs;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentOrderService
    {
        /// <summary>
        /// Validates the purchase, creates (or reuses) an internal order, creates a Razorpay order when needed,
        /// and returns Checkout details. Does not grant entitlements.
        /// </summary>
        Task<CreatePaymentOrderResponse> CreateOrderAsync(
            Guid userId,
            CreatePaymentOrderRequest request,
            string? correlationId,
            CancellationToken cancellationToken = default);
    }
}
