using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    public sealed class PaymentQueryService : IPaymentQueryService
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 50;

        private readonly IPaymentQueryRepository _repository;

        public PaymentQueryService(IPaymentQueryRepository repository)
        {
            _repository = repository;
        }

        public async Task<(IReadOnlyList<PaymentHistoryItemResponse> Items, PaginationMeta Meta)> GetHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

            var (orders, total) = await _repository.ListOrdersForUserAsync(userId, page, pageSize, cancellationToken);
            var items = new List<PaymentHistoryItemResponse>(orders.Count);
            foreach (var order in orders)
            {
                items.Add(await MapHistoryItemAsync(order, cancellationToken));
            }

            return (items, PaginationMeta.Create(page, pageSize, total));
        }

        public async Task<PaymentOrderStatusResponse> GetOrderAsync(
            Guid userId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var order = await _repository.FindOwnedOrderAsync(userId, orderId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Payment order not found.");

            var attempts = await _repository.ListAttemptsForOrderAsync(order.Id, cancellationToken);
            var planCode = await _repository.ResolvePlanCodeAsync(order.PlanId, cancellationToken) ?? string.Empty;
            var planName = await _repository.ResolvePlanNameAsync(order.PlanId, cancellationToken) ?? string.Empty;

            return new PaymentOrderStatusResponse
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                PurchaseType = order.PurchaseType.ToString(),
                PlanCode = planCode,
                PlanName = planName,
                AmountInPaise = order.AmountInPaise,
                Currency = order.Currency,
                RazorpayOrderId = order.RazorpayOrderId,
                ContentType = order.ContentType?.ToString().ToLowerInvariant(),
                ContentId = order.ContentId,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                PaidAt = order.PaidAt,
                ExpiresAt = order.ExpiresAt,
                Attempts = attempts.Select(attempt => new PaymentAttemptStatusItem
                {
                    AttemptId = attempt.Id,
                    RazorpayPaymentId = attempt.RazorpayPaymentId,
                    Status = attempt.Status.ToString(),
                    RazorpayStatus = attempt.RazorpayStatus,
                    PaymentMethod = attempt.PaymentMethod,
                    CreatedAt = attempt.CreatedAt,
                    CapturedAt = attempt.CapturedAt,
                    FailedAt = attempt.FailedAt
                }).ToList()
            };
        }

        private async Task<PaymentHistoryItemResponse> MapHistoryItemAsync(
            Domain.Entities.PaymentOrder order,
            CancellationToken cancellationToken)
        {
            return new PaymentHistoryItemResponse
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                PurchaseType = order.PurchaseType.ToString(),
                PlanCode = await _repository.ResolvePlanCodeAsync(order.PlanId, cancellationToken) ?? string.Empty,
                PlanName = await _repository.ResolvePlanNameAsync(order.PlanId, cancellationToken) ?? string.Empty,
                AmountInPaise = order.AmountInPaise,
                Currency = order.Currency,
                RazorpayOrderId = order.RazorpayOrderId,
                ContentType = order.ContentType?.ToString().ToLowerInvariant(),
                ContentId = order.ContentId,
                CreatedAt = order.CreatedAt,
                PaidAt = order.PaidAt,
                ExpiresAt = order.ExpiresAt
            };
        }
    }
}
