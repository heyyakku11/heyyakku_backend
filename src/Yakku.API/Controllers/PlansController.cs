using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Responses;
using Yakku.Application.Plans.DTOs;
using Yakku.Application.Plans.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>Subscription plans. Intentionally unauthenticated.</summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/plans")]
    public class PlansController : ControllerBase
    {
        private readonly IPlanService _service;

        public PlansController(IPlanService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("List Plans")]
        [EndpointDescription("Lists all Yakku plans in plan order (id, code, name, price, currency).")]
        [ProducesResponseType(typeof(ApiResponse<List<PlanResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var result = await _service.ListAsync(cancellationToken);
            return Ok(ApiResponse.Ok(result, "Plans retrieved successfully"));
        }

        [HttpPatch("{id:guid}")]
        [EndpointSummary("Update Plan")]
        [EndpointDescription("Updates the name, price or active state of a plan. Only the fields sent are changed.")]
        [ProducesResponseType(typeof(ApiResponse<PlanResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken)
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Plan updated successfully"));
        }
    }
}
