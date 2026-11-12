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
    }
}
