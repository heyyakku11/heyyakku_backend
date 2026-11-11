using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>Admin management of just-for-fun cards (no live week; they use the just-for-fun theme).</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku/fun-cards")]
    public class AdminFunCardsController : ControllerBase
    {
        private readonly IAdminFunCardService _service;

        public AdminFunCardsController(IAdminFunCardService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("List Fun Cards (Admin)")]
        [EndpointDescription("Lists every just-for-fun card, active or not. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminFunCardResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var result = await _service.ListAsync(cancellationToken);
            return Ok(ApiResponse.Ok(result, "Cards retrieved successfully"));
        }

        [HttpPost]
        [EndpointSummary("Create Fun Card (Admin)")]
        [EndpointDescription("Creates a just-for-fun card with 2 or 3 options. The look comes from the just-for-fun theme. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<AdminFunCardResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] SaveFunCardRequest request, CancellationToken cancellationToken)
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Card created successfully"));
        }

        [HttpPut("{id:guid}")]
        [EndpointSummary("Update Fun Card (Admin)")]
        [EndpointDescription("Updates a just-for-fun card. The wording can only change while nobody has made the card. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<AdminFunCardResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveFunCardRequest request, CancellationToken cancellationToken)
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card updated successfully"));
        }

        [HttpDelete("{id:guid}")]
        [EndpointSummary("Delete Fun Card (Admin)")]
        [EndpointDescription("Deletes a just-for-fun card that nobody has made. Admin only.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
