using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Guests;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.DTOs;

namespace Yakku.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/guests")]
    public class GuestsController : ControllerBase
    {
        private readonly GuestCookieService _guestCookieService;

        public GuestsController(GuestCookieService guestCookieService)
        {
            _guestCookieService = guestCookieService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<GuestResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var result = await _guestCookieService.EnsureAsync(HttpContext, cancellationToken);

            var response = new GuestResponse
            {
                GuestId = result.GuestId,
                ExpiresAt = result.ExpiresAt
            };

            return Ok(ApiResponse.Ok(response, "Guest created successfully"));
        }
    }
}
