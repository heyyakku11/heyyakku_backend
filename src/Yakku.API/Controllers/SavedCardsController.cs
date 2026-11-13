using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.SavedCards.DTOs;
using Yakku.Application.SavedCards.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>The cards a signed-in user saved: polls from other users and fun cards, in one list.</summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/saved-cards")]
    public class SavedCardsController : ControllerBase
    {
        private readonly ISavedCardService _service;

        public SavedCardsController(ISavedCardService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("List Saved Cards")]
        [EndpointDescription("Lists the cards the signed-in user saved, polls and fun cards together, most recently saved first. Each item has cardType (\"poll\" or \"fun_card\") and the matching poll or funCard. Cursor pagination: pass the cursor from the previous response's meta. Returns 400 for an invalid cursor.")]
        [ProducesResponseType(typeof(ApiResponse<List<SavedCardResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List([FromQuery] string? cursor, CancellationToken cancellationToken)
        {
            var result = await _service.ListAsync(User.GetRequiredUserId(), cursor, cancellationToken);
            return Ok(ApiResponse.Ok(result.Items, "Saved cards retrieved successfully", result.Meta));
        }

        [HttpPut("{cardType}/{id:guid}")]
        [EndpointSummary("Save Card")]
        [EndpointDescription("Saves a card: cardType is \"poll\" or \"fun_card\". Idempotent. A poll must be someone else's (400 for your own, 404 when it does not exist). A fun card must be active (404 otherwise, weekly checks included). Returns 400 for an unknown cardType.")]
        [ProducesResponseType(typeof(ApiResponse<SavedCardStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Save(string cardType, Guid id, CancellationToken cancellationToken)
        {
            var type = ParseType(cardType);
            var result = await _service.SaveAsync(User.GetRequiredUserId(), type, id, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card saved successfully"));
        }

        [HttpDelete("{cardType}/{id:guid}")]
        [EndpointSummary("Unsave Card")]
        [EndpointDescription("Removes a card from the signed-in user's saved cards: cardType is \"poll\" or \"fun_card\". Idempotent.")]
        [ProducesResponseType(typeof(ApiResponse<SavedCardStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Unsave(string cardType, Guid id, CancellationToken cancellationToken)
        {
            var type = ParseType(cardType);
            var result = await _service.UnsaveAsync(User.GetRequiredUserId(), type, id, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card unsaved successfully"));
        }

        private static Yakku.Domain.Enums.SavedCardType ParseType(string cardType)
        {
            if (!SavedCardTypes.TryParse(cardType, out var type))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "cardType must be \"poll\" or \"fun_card\".",
                    "cardType");
            }

            return type;
        }
    }
}
