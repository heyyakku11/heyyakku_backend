using FluentValidation;
using Microsoft.Extensions.Logging;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    public sealed class PaymentOrderService : IPaymentOrderService
    {
        private readonly IPaymentOrderRepository _repository;
        private readonly IRazorpayClient _razorpay;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly RazorpayOptions _razorpayOptions;
        private readonly PaymentOptions _paymentOptions;
        private readonly IValidator<CreatePaymentOrderRequest> _validator;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymentOrderService> _logger;
        private readonly IConversationTopUpGate _topUpGate;

        public PaymentOrderService(
            IPaymentOrderRepository repository,
            IRazorpayClient razorpay,
            IPaymentLogWriter paymentLogs,
            RazorpayOptions razorpayOptions,
            PaymentOptions paymentOptions,
            IValidator<CreatePaymentOrderRequest> validator,
            TimeProvider time,
            ILogger<PaymentOrderService> logger,
            IConversationTopUpGate topUpGate)
        {
            _repository = repository;
            _razorpay = razorpay;
            _paymentLogs = paymentLogs;
            _razorpayOptions = razorpayOptions;
            _paymentOptions = paymentOptions;
            _validator = validator;
            _time = time;
            _logger = logger;
            _topUpGate = topUpGate;
        }

        public async Task<CreatePaymentOrderResponse> CreateOrderAsync(
            Guid userId,
            CreatePaymentOrderRequest request,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            if (!_razorpay.IsEnabled || !_razorpayOptions.Enabled)
            {
                throw new AppException(
                    503,
                    ApiErrorCodes.PaymentProviderDisabled,
                    "Payments are temporarily unavailable.");
            }

            if (!await _repository.UserExistsAsync(userId, cancellationToken))
            {
                throw new AppException(401, ApiErrorCodes.Unauthorized, "Unauthorized.");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var idempotencyKey = request.IdempotencyKey!.Trim();

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.PaymentOrderCreationRequested,
                    PaymentLogSources.Api,
                    PaymentLogStatus.Information,
                    "Payment order creation requested.",
                    UserId: userId,
                    CorrelationId: correlationId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["planCode"] = request.PlanCode,
                        ["idempotencyKey"] = idempotencyKey
                    }),
                cancellationToken);

            var existing = await _repository.FindByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return await ReuseOrRefreshAsync(existing, correlationId, now, cancellationToken);
            }

            var plan = await _repository.GetActivePurchasablePlanByCodeAsync(request.PlanCode, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Plan not found or inactive.", "planCode");

            if (plan.PriceMinor <= 0)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This plan cannot be purchased.",
                    "planCode");
            }

            var currency = string.IsNullOrWhiteSpace(plan.Currency)
                ? _paymentOptions.Currency
                : plan.Currency;

            if (!string.Equals(currency, _paymentOptions.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    $"Only {_paymentOptions.Currency} payments are supported.",
                    "planCode");
            }

            PaymentOrder order;
            if (plan.Id == Plan.ThisCardId || plan.Code == Plan.ThisCardCode)
            {
                order = await CreateCardUnlockOrderAsync(
                    userId, plan, request, currency, idempotencyKey, correlationId, now, cancellationToken);
            }
            else if (Plan.IsUserPlanCode(plan.Code))
            {
                order = await CreateUserPlanOrderAsync(
                    userId, plan, request, currency, idempotencyKey, correlationId, now, cancellationToken);
            }
            else if (Plan.IsConversationTopUpCode(plan.Code))
            {
                order = await CreateConversationTopUpOrderAsync(
                    userId, plan, request, currency, idempotencyKey, correlationId, now, cancellationToken);
            }
            else
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This plan cannot be purchased.",
                    "planCode");
            }

            var saved = await _repository.TryAddAsync(order, cancellationToken);
            if (saved is null)
            {
                // Concurrent duplicate idempotency key: return the winner's order.
                var raced = await _repository.FindByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken)
                    ?? throw new AppException(
                        409,
                        ApiErrorCodes.Conflict,
                        "A payment order with this idempotency key already exists.",
                        "idempotencyKey");
                return await ReuseOrRefreshAsync(raced, correlationId, now, cancellationToken);
            }

            await AttachRazorpayOrderAsync(saved, plan, correlationId, now, cancellationToken);
            return ToResponse(saved, plan);
        }

        private async Task<PaymentOrder> CreateUserPlanOrderAsync(
            Guid userId,
            Plan plan,
            CreatePaymentOrderRequest request,
            string currency,
            string idempotencyKey,
            string? correlationId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (request.ContentType is not null || request.ContentId is not null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "contentType and contentId are only used for this_card purchases.");
            }

            var projection = await _repository.GetUserPlanProjectionAsync(userId, cancellationToken);
            if (projection is null)
            {
                throw new AppException(401, ApiErrorCodes.Unauthorized, "Unauthorized.");
            }

            var current = PurchaseEligibility.ResolveEffectivePaidPlan(
                projection.Value.PlanId, projection.Value.ExpiresAt, now);
            var rejection = PurchaseEligibility.RejectUserPlanPurchase(current, plan.Code);
            if (rejection is not null)
            {
                var code = current?.Code == Plan.YakkuLoveCode && plan.Code == Plan.YakkuLiteCode
                    ? ApiErrorCodes.PlanDowngradeNotAllowed
                    : ApiErrorCodes.PlanAlreadyActive;
                throw new AppException(409, code, rejection, "planCode");
            }

            var snapshot = PlanPurchaseSnapshot.FromPlan(plan).ToJson();
            return PaymentOrder.CreateUserPlan(
                userId,
                plan.Id,
                plan.PriceMinor,
                currency,
                snapshot,
                idempotencyKey,
                now,
                _paymentOptions.OrderLifetime,
                correlationId);
        }

        private async Task<PaymentOrder> CreateConversationTopUpOrderAsync(
            Guid userId,
            Plan plan,
            CreatePaymentOrderRequest request,
            string currency,
            string idempotencyKey,
            string? correlationId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (request.ContentType is not null || request.ContentId is not null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "contentType and contentId are not used for a conversation top-up.");
            }

            if (request.ConversationId is not Guid conversationId || conversationId == Guid.Empty)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "conversationId is required to top up a conversation.",
                    "conversationId");
            }

            // Eligibility is checked here and again at fulfillment, so a chat that changed in between is not granted
            // silently.
            await _topUpGate.EnsureCanTopUpAsync(userId, conversationId, cancellationToken);

            var snapshot = PlanPurchaseSnapshot.FromPlan(plan).ToJson();
            return PaymentOrder.CreateConversationTopUp(
                userId,
                plan.Id,
                conversationId,
                plan.PriceMinor,
                currency,
                snapshot,
                idempotencyKey,
                now,
                _paymentOptions.OrderLifetime,
                correlationId);
        }

        private async Task<PaymentOrder> CreateCardUnlockOrderAsync(
            Guid userId,
            Plan plan,
            CreatePaymentOrderRequest request,
            string currency,
            string idempotencyKey,
            string? correlationId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ContentType) || request.ContentId is not Guid contentId)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "contentType and contentId are required for this_card purchases.");
            }

            var contentType = ParseContentType(request.ContentType);
            if (!await _repository.OwnsCardAsync(userId, contentType, contentId, cancellationToken))
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Card not found");
            }

            if (await _repository.HasPermanentUnlockAsync(userId, contentType, contentId, cancellationToken))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.CardAlreadyUnlocked,
                    "You already own permanent access to this card.",
                    "contentId");
            }

            var snapshot = PlanPurchaseSnapshot.FromPlan(plan).ToJson();
            return PaymentOrder.CreateCardUnlock(
                userId,
                plan.Id,
                contentType,
                contentId,
                plan.PriceMinor,
                currency,
                snapshot,
                idempotencyKey,
                now,
                _paymentOptions.OrderLifetime,
                correlationId);
        }

        private async Task<CreatePaymentOrderResponse> ReuseOrRefreshAsync(
            PaymentOrder existing,
            string? correlationId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (existing.Status == PaymentOrderStatus.Paid)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This payment order was already paid.",
                    "idempotencyKey");
            }

            if (existing.Status is PaymentOrderStatus.Cancelled)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This payment order was cancelled. Use a new idempotency key.",
                    "idempotencyKey");
            }

            var plan = await _repository.GetActivePurchasablePlanAsync(existing.PlanId ?? Guid.Empty, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Plan not found or inactive.", "planCode");

            var stillOpen = existing.Status is PaymentOrderStatus.Created or PaymentOrderStatus.Pending
                            or PaymentOrderStatus.Failed;
            var hasGatewayOrder = !string.IsNullOrWhiteSpace(existing.RazorpayOrderId);
            var notExpired = existing.ExpiresAt > now;

            if (stillOpen && hasGatewayOrder && notExpired)
            {
                return ToResponse(existing, plan);
            }

            if (!stillOpen && existing.Status == PaymentOrderStatus.Expired)
            {
                // Same idempotency key: refresh the gateway order on the existing row.
            }

            await AttachRazorpayOrderAsync(existing, plan, correlationId, now, cancellationToken);
            return ToResponse(existing, plan);
        }

        private async Task AttachRazorpayOrderAsync(
            PaymentOrder order,
            Plan plan,
            string? correlationId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var captureAutomatic = _paymentOptions.ParseCaptureMode() == PaymentCaptureMode.Automatic;
            var gateway = await _razorpay.CreateOrderAsync(
                new RazorpayCreateOrderRequest(
                    order.AmountInPaise,
                    order.Currency,
                    order.Id.ToString("N"),
                    captureAutomatic,
                    correlationId),
                cancellationToken);

            if (!gateway.Succeeded || string.IsNullOrWhiteSpace(gateway.RazorpayOrderId))
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentOrderCreationFailed,
                        PaymentLogSources.Api,
                        PaymentLogStatus.Failure,
                        "Razorpay order creation failed.",
                        PaymentOrderId: order.Id,
                        UserId: order.UserId,
                        CorrelationId: correlationId,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["errorCode"] = gateway.ErrorCode,
                            ["errorMessage"] = gateway.ErrorMessage
                        }),
                    cancellationToken);

                _logger.LogWarning(
                    "Razorpay order creation failed for internal order {OrderId}. ErrorCode {ErrorCode}.",
                    order.Id,
                    gateway.ErrorCode);

                throw new AppException(
                    502,
                    ApiErrorCodes.PaymentGatewayError,
                    "Unable to start checkout with the payment provider. Please try again.");
            }

            if (gateway.AmountInPaise is int amount && amount != order.AmountInPaise)
            {
                throw new AppException(
                    502,
                    ApiErrorCodes.PaymentGatewayError,
                    "Payment provider returned an unexpected amount.");
            }

            order.AttachRazorpayOrder(gateway.RazorpayOrderId, now, _paymentOptions.OrderLifetime);
            await _repository.SaveAsync(cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.PaymentOrderCreated,
                    PaymentLogSources.Api,
                    PaymentLogStatus.Success,
                    "Payment order created with Razorpay.",
                    PaymentOrderId: order.Id,
                    UserId: order.UserId,
                    CorrelationId: correlationId ?? order.CorrelationId,
                    RazorpayOrderId: order.RazorpayOrderId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["planCode"] = plan.Code,
                        ["amountInPaise"] = order.AmountInPaise.ToString()
                    }),
                cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.CheckoutInitiated,
                    PaymentLogSources.Api,
                    PaymentLogStatus.Information,
                    "Checkout details issued to client.",
                    PaymentOrderId: order.Id,
                    UserId: order.UserId,
                    CorrelationId: correlationId ?? order.CorrelationId,
                    RazorpayOrderId: order.RazorpayOrderId),
                cancellationToken);
        }

        private CreatePaymentOrderResponse ToResponse(PaymentOrder order, Plan plan)
        {
            if (string.IsNullOrWhiteSpace(order.RazorpayOrderId))
            {
                throw new AppException(
                    502,
                    ApiErrorCodes.PaymentGatewayError,
                    "Unable to start checkout with the payment provider. Please try again.");
            }

            return new CreatePaymentOrderResponse
            {
                OrderId = order.Id,
                RazorpayOrderId = order.RazorpayOrderId,
                RazorpayKeyId = _razorpayOptions.KeyId,
                AmountInPaise = order.AmountInPaise,
                Currency = order.Currency,
                PurchaseType = order.PurchaseType.ToString(),
                PlanCode = plan.Code,
                PlanName = plan.Name,
                Status = order.Status.ToString(),
                ExpiresAt = order.ExpiresAt,
                ContentType = order.ContentType switch
                {
                    ShareContentType.Poll => "poll",
                    ShareContentType.Check => "check",
                    _ => null
                },
                ContentId = order.ContentId,
                ConversationId = order.ConversationId
            };
        }

        private static ShareContentType ParseContentType(string contentType)
        {
            return contentType.Trim().ToLowerInvariant() switch
            {
                "poll" => ShareContentType.Poll,
                "check" => ShareContentType.Check,
                _ => throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "contentType must be poll or check.",
                    "contentType")
            };
        }
    }
}
