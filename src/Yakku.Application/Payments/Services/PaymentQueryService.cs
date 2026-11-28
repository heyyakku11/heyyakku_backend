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

        private async Task<PaymentHistoryItemResponse> MapHistoryItemAsync(
            Domain.Entities.PaymentOrder order,
            CancellationToken cancellationToken)
        {
            return new PaymentHistoryItemResponse
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                PlanName = await _repository.ResolvePlanNameAsync(order.PlanId, cancellationToken) ?? string.Empty,
                AmountInPaise = order.AmountInPaise,
                Currency = order.Currency,
                RazorpayOrderId = order.RazorpayOrderId,
                ContentType = order.ContentType?.ToString().ToLowerInvariant(),
                ContentId = order.ContentId,
                ConversationId = order.ConversationId,
                CreatedAt = order.CreatedAt
            };
        }
    }
}
