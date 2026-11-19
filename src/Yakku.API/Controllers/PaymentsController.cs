using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;

namespace Yakku.API.Controllers
{
    /// <summary>One-time Razorpay checkout for plans and permanent card unlocks. Registered users only.</summary>
    [Authorize]
    [ServiceFilter(typeof(LiveSessionFilter))]
    [ApiController]
    [Route("api/v1/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentOrderService _orders;
        private readonly IPaymentVerificationService _verification;
        private readonly IPaymentQueryService _queries;

        public PaymentsController(
            IPaymentOrderService orders,
            IPaymentVerificationService verification,
            IPaymentQueryService queries)
        {
            _orders = orders;
            _verification = verification;
            _queries = queries;
        }

        [HttpPost("orders")]
        [EnableRateLimiting("payments-write")]
        [EndpointSummary("Create a payment order")]
        [EndpointDescription(
            "Creates an internal payment order and a Razorpay order for Checkout. Prices come from the plans catalog; " +
            "the client never supplies the amount. Guests are rejected. Duplicate active Lite/Love purchases and " +
            "duplicate permanent card unlocks are rejected. Retries with the same idempotencyKey reuse the same order. " +
            "Does not grant entitlements — call verify after payment.")]
        [ProducesResponseType(typeof(ApiResponse<CreatePaymentOrderResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreatePaymentOrderRequest request,
            CancellationToken cancellationToken)
        {
            var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var result = await _orders.CreateOrderAsync(
                User.GetRequiredUserId(),
                request,
                correlationId,
                cancellationToken);
            return Ok(ApiResponse.Ok(result, "Payment order created"));
        }

        [HttpPost("verify")]
        [EnableRateLimiting("payments-write")]
        [EndpointSummary("Verify a Razorpay payment")]
        [EndpointDescription(
            "Verifies the Checkout signature, confirms the payment is captured with Razorpay, and idempotently " +
            "activates the purchased plan or permanent card unlock. The frontend success callback alone is not trusted.")]
        [ProducesResponseType(typeof(ApiResponse<VerifyPaymentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Verify(
            [FromBody] VerifyPaymentRequest request,
            CancellationToken cancellationToken)
        {
            var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var result = await _verification.VerifyAsync(
                User.GetRequiredUserId(),
                request,
                correlationId,
                cancellationToken);
            return Ok(ApiResponse.Ok(result, result.AlreadyFulfilled ? "Payment already verified" : "Payment verified"));
        }

        [HttpGet("history")]
        [EnableRateLimiting("payments-write")]
        [EndpointSummary("Payment history")]
        [EndpointDescription(
            "Lists the caller's payment orders newest first. Paginated with page and pageSize (default 10, max 50).")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PaymentHistoryItemResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> History(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = PaymentQueryService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _queries.GetHistoryAsync(
                User.GetRequiredUserId(), page, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Payment history", meta));
        }

        [HttpGet("orders/{orderId:guid}")]
        [EnableRateLimiting("payments-write")]
        [EndpointSummary("Payment order status")]
        [EndpointDescription("Returns one of the caller's payment orders and its gateway attempts.")]
        [ProducesResponseType(typeof(ApiResponse<PaymentOrderStatusResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken cancellationToken)
        {
            var result = await _queries.GetOrderAsync(User.GetRequiredUserId(), orderId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Payment order"));
        }
    }
}
