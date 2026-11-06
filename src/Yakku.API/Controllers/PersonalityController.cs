using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Personality.DTOs;
using Yakku.Application.Personality.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1")]
    public class PersonalityController : ControllerBase
    {
        private readonly IPersonalityService _service;

        public PersonalityController(IPersonalityService service)
        {
            _service = service;
        }

        [AllowAnonymous]
        [HttpGet("personality/traits")]
        [EndpointSummary("List Personality Traits")]
        [EndpointDescription("Returns the active predefined personality traits, in display order. Public: no sign-in needed.")]
        [ProducesResponseType(typeof(ApiResponse<List<PersonalityTraitResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTraits(CancellationToken cancellationToken)
        {
            var result = await _service.GetTraitsAsync(cancellationToken);
            return Ok(ApiResponse.Ok(result, "Personality traits retrieved successfully"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpGet("personality-cards/me")]
        [EndpointSummary("Get My Personality Card")]
        [EndpointDescription("Returns the signed-in user's card: status, self-selected traits, vote progress and completion time.")]
        [ProducesResponseType(typeof(ApiResponse<PersonalityCardResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        {
            var result = await _service.GetMyCardAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Personality card retrieved successfully"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpPost("personality-cards/me/self-assessment")]
        [EndpointSummary("Submit Self-Assessment")]
        [EndpointDescription("Picks exactly 3 distinct active traits to describe yourself. Allowed once, while the card is a draft; the card then starts collecting votes.")]
        [ProducesResponseType(typeof(ApiResponse<PersonalityCardResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SubmitSelfAssessment(
            [FromBody] SubmitTraitsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _service.SubmitSelfAssessmentAsync(User.GetRequiredUserId(), request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Self-assessment submitted successfully"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpGet("personality-cards/{shareCode}")]
        [EndpointSummary("Get A Shared Personality Card")]
        [EndpointDescription("Opens a card by its share code, as a voter sees it. Cards still in draft are not found. The owner's own picks and any voter details are never included.")]
        [ProducesResponseType(typeof(ApiResponse<SharedPersonalityCardResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetShared(string shareCode, CancellationToken cancellationToken)
        {
            var result = await _service.GetSharedCardAsync(shareCode, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Personality card retrieved successfully"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpPost("personality-cards/{shareCode}/votes")]
        [EndpointSummary("Vote On A Personality Card")]
        [EndpointDescription("Picks exactly 3 distinct active traits that describe the card's owner. One vote per user per card, never on your own card, and only while the card is collecting votes. The vote that reaches the threshold completes the card.")]
        [ProducesResponseType(typeof(ApiResponse<PersonalityVoteResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Vote(
            string shareCode,
            [FromBody] SubmitTraitsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _service.VoteAsync(shareCode, User.GetRequiredUserId(), request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Vote submitted successfully"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpGet("personality-cards/{shareCode}/results")]
        [EndpointSummary("Get Personality Results")]
        [EndpointDescription("You vs the crowd: your 3 traits, the crowd's top 3, and what they share. Owner only, and only once the card is completed (409 before that). No voter is ever identified.")]
        [ProducesResponseType(typeof(ApiResponse<PersonalityResultsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> GetResults(string shareCode, CancellationToken cancellationToken)
        {
            var result = await _service.GetResultsAsync(shareCode, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Personality results retrieved successfully"));
        }
    }
}
