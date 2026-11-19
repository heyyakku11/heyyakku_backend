using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>Razorpay webhook ingress. Signature is verified against the raw body before any JSON parse.</summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/webhooks")]
    public class RazorpayWebhooksController : ControllerBase
    {
        private readonly IRazorpayWebhookReceiver _receiver;

        public RazorpayWebhooksController(IRazorpayWebhookReceiver receiver)
        {
            _receiver = receiver;
        }

        [HttpPost("razorpay")]
        [EnableRateLimiting("payments-write")]
        [EndpointSummary("Razorpay webhook")]
        [EndpointDescription(
            "Receives Razorpay webhooks. Verifies X-Razorpay-Signature over the exact raw body using the webhook secret, " +
            "persists the event for idempotent background processing, then returns 200. Access is granted only for " +
            "captured payments via the same fulfillment path as Checkout verify.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Receive(CancellationToken cancellationToken)
        {
            byte[] rawBody;
            await using (var buffer = new MemoryStream())
            {
                await Request.Body.CopyToAsync(buffer, cancellationToken);
                rawBody = buffer.ToArray();
            }

            var signature = Request.Headers["X-Razorpay-Signature"].ToString();
            var eventId = Request.Headers["X-Razorpay-Event-Id"].ToString();
            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventId = Request.Headers["x-razorpay-event-id"].ToString();
            }

            var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var result = await _receiver.ReceiveAsync(
                rawBody,
                signature,
                string.IsNullOrWhiteSpace(eventId) ? null : eventId,
                correlationId,
                cancellationToken);

            if (!result.Accepted)
            {
                var status = result.ErrorCode == ApiErrorCodes.PaymentProviderDisabled
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status400BadRequest;

                return StatusCode(
                    status,
                    ApiResponse.Fail(
                        result.ErrorMessage ?? "Webhook rejected.",
                        [
                            new ApiError
                            {
                                Code = result.ErrorCode ?? ApiErrorCodes.ValidationError,
                                Message = result.ErrorMessage ?? "Webhook rejected."
                            }
                        ]));
            }

            return Ok(ApiResponse.Ok(
                new { duplicate = result.Duplicate, webhookEventId = result.WebhookEventId },
                result.Duplicate ? "Webhook already received" : "Webhook accepted"));
        }
    }
}
