using Microsoft.AspNetCore.Mvc;
using Yakku.API.Middleware;
using Yakku.Application.Audit;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.API.Configuration
{
    internal static class InvalidModelStateFactory
    {
        public static IActionResult Create(ActionContext context)
        {
            var http = context.HttpContext;
            var audit = http.RequestServices.GetService<IAuditLogWriter>();
            if (audit is not null)
            {
                var errorCount = context.ModelState.ErrorCount;
                var first = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .Select(entry => new
                    {
                        Field = entry.Key,
                        Message = entry.Value!.Errors[0].ErrorMessage
                    })
                    .FirstOrDefault();

                _ = audit.WriteSystemAsync(
                    new SystemLogEntry(
                        SystemLogCategory.Exception,
                        SystemLogSeverity.Warning,
                        nameof(InvalidModelStateFactory),
                        "Model validation failed.",
                        ExceptionType: "ModelState",
                        HttpMethod: http.Request.Method,
                        Path: http.Request.Path.Value,
                        StatusCode: StatusCodes.Status400BadRequest,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["errorCount"] = errorCount.ToString(),
                            ["firstField"] = AuditSanitizer.SanitizeCode(first?.Field, 64),
                            ["firstError"] = AuditSanitizer.SanitizeText(first?.Message, 200)
                        }),
                    CancellationToken.None);
            }

            var response = ApiResponse.Fail(
                "Validation failed",
                ApiErrorMapper.FromModelState(context.ModelState));

            return new BadRequestObjectResult(response);
        }
    }
}
