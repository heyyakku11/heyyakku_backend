using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Community.DTOs;
using Yakku.Application.Community.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/community")]
    public class CommunityController : ControllerBase
    {
        private readonly ICommunityWaitlistService _waitlist;

        public CommunityController(ICommunityWaitlistService waitlist)
        {
            _waitlist = waitlist;
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpPost("notify-me")]
        [EndpointSummary("Notify Me When Community Is Live")]
        [EndpointDescription("Adds the signed-in user to the queue of people to be notified when the community goes live. Safe to call again: a user already in the queue gets 200 with alreadyJoined true, a new one gets 201.")]
        [ProducesResponseType(typeof(ApiResponse<CommunityWaitlistResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<CommunityWaitlistResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> NotifyMe(CancellationToken cancellationToken)
        {
            var result = await _waitlist.JoinAsync(User.GetRequiredUserId(), cancellationToken);
            if (result.AlreadyJoined)
            {
                return Ok(ApiResponse.Ok(result, "You are already on the list"));
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "We will notify you when the community is live"));
        }
    }
}
