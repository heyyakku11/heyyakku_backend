using Microsoft.AspNetCore.Mvc;
using Yakku.API.Middleware;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Configuration
{
    internal static class InvalidModelStateFactory
    {
        public static IActionResult Create(ActionContext context)
        {
            var response = ApiResponse.Fail(
                "Validation failed",
                ApiErrorMapper.FromModelState(context.ModelState));

            return new BadRequestObjectResult(response);
        }
    }
}
