using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>Admin payment operations. Refunds never auto-revoke entitlements.</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku/payments")]
    public class AdminPaymentsController : ControllerBase
    {
        private readonly IPaymentRefundService _refunds;

        public AdminPaymentsController(IPaymentRefundService refunds)
        {
            _refunds = refunds;
        }

        [HttpPost("attempts/{paymentAttemptId:guid}/refunds")]
        [EndpointSummary("Refund a captured payment (Admin)")]
        [EndpointDescription(
            "Creates a Razorpay refund for a captured payment attempt. Access (plan entitlements / permanent unlocks) " +
            "is not revoked automatically — use a separate admin action when product requires clawback.")]
        [ProducesResponseType(typeof(ApiResponse<PaymentRefundResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateRefund(
            Guid paymentAttemptId,
            [FromBody] CreatePaymentRefundRequest request,
            CancellationToken cancellationToken)
        {
            var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var result = await _refunds.CreateRefundAsync(
                paymentAttemptId,
                request ?? new CreatePaymentRefundRequest(),
                correlationId,
                cancellationToken);
            return Ok(ApiResponse.Ok(result, "Refund processed"));
        }
    }
}
