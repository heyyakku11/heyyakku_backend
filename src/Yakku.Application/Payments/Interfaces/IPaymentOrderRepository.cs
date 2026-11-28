using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentOrderRepository
    {
        Task<Plan?> GetActivePurchasablePlanAsync(Guid planId, CancellationToken cancellationToken = default);

        Task<Plan?> GetActivePurchasablePlanByCodeAsync(string planCode, CancellationToken cancellationToken = default);

        Task<PaymentOrder?> FindByIdempotencyKeyAsync(
            Guid userId,
            string idempotencyKey,
            CancellationToken cancellationToken = default);

        Task<PaymentOrder?> FindByRazorpayOrderIdAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default);

        Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<(Guid PlanId, DateTime? ActivatedAt, DateTime? ExpiresAt)?> GetUserPlanProjectionAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> OwnsCardAsync(
            Guid userId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        Task<bool> HasPermanentUnlockAsync(
            Guid userId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>Inserts the order. Returns null when the user/idempotency key already exists.</summary>
        Task<PaymentOrder?> TryAddAsync(PaymentOrder order, CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
