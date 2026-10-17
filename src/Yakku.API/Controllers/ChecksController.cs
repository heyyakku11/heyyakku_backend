using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/checks")]
    public class ChecksController : ControllerBase
    {
        private readonly ICheckService _checkService;

        public ChecksController(ICheckService checkService)
        {
            _checkService = checkService;
        }

        /// <summary>Every live check (this week and earlier), earliest first, with isCurrent and the caller used flag.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<CheckResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var result = await _checkService.GetChecksAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Checks retrieved successfully"));
        }

        /// <summary>Only the checks scheduled for the current week.</summary>
        [HttpGet("current")]
        [ProducesResponseType(typeof(ApiResponse<List<CheckResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
        {
            var result = await _checkService.GetCurrentAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Current checks retrieved successfully"));
        }

        /// <summary>Makes a live check (current or past week) into the caller's own shareable check. Once per check.</summary>
        [HttpPost("{id:guid}/make")]
        [ProducesResponseType(typeof(ApiResponse<UserCheckResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Make(Guid id, CancellationToken cancellationToken)
        {
            var result = await _checkService.MakeAsync(id, User.GetRequiredUserId(), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Check created successfully"));
        }

        /// <summary>The caller's made checks, kept separate from their polls.</summary>
        [HttpGet("mine")]
        [ProducesResponseType(typeof(ApiResponse<List<UserCheckResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        {
            var result = await _checkService.GetMineAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Your checks retrieved successfully"));
        }

        [HttpGet("mine/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<UserCheckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMineById(Guid id, CancellationToken cancellationToken)
        {
            var result = await _checkService.GetMineByIdAsync(id, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Check retrieved successfully"));
        }

        /// <summary>Opens a shared check by its share code.</summary>
        [HttpGet("shared/{shareCode}")]
        [ProducesResponseType(typeof(ApiResponse<UserCheckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetShared(string shareCode, CancellationToken cancellationToken)
        {
            var result = await _checkService.GetSharedAsync(shareCode, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Check retrieved successfully"));
        }

        [EnableRateLimiting("poll-votes")]
        [HttpPost("shared/{shareCode}/votes")]
        [ProducesResponseType(typeof(ApiResponse<CheckVoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Vote(
            string shareCode,
            [FromBody] CastCheckVoteRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _checkService.VoteAsync(shareCode, User.GetRequiredUserId(), request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Vote submitted successfully"));
        }
    }
}
