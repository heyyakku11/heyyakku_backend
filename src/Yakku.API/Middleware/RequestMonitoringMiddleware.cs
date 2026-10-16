using System.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Domain.Enums;

namespace Yakku.API.Middleware
{
    /// <summary>
    /// Records slow requests and 5xx responses in system_logs. Unhandled exceptions are recorded by
    /// <see cref="GlobalExceptionHandler"/> and rate-limit rejections where the limiter rejects, so neither is
    /// duplicated here. Only the method, the id-normalised path and timings are kept: no query string, headers or body.
    /// </summary>
    internal sealed class RequestMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly TimeSpan _slowThreshold;

        public RequestMonitoringMiddleware(RequestDelegate next, TimeSpan slowThreshold)
        {
            _next = next;
            _slowThreshold = slowThreshold;
        }

        public async Task InvokeAsync(HttpContext context, IAuditLogWriter audit)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                await RecordAsync(context, audit, stopwatch.Elapsed);
            }
        }

        private async Task RecordAsync(HttpContext context, IAuditLogWriter audit, TimeSpan elapsed)
        {
            var status = context.Response.StatusCode;
            var slow = elapsed >= _slowThreshold;
            // An exception that escapes is still 200 here (the exception handler sets 500 afterwards) and is
            // recorded by GlobalExceptionHandler, so this only sees handled 5xx results such as a 502 AppException.
            var failed = status >= 500;
            if (!slow && !failed)
            {
                return;
            }

            await audit.WriteSystemAsync(
                new SystemLogEntry(
                    slow ? SystemLogCategory.Latency : SystemLogCategory.Failure,
                    failed ? SystemLogSeverity.Error : SystemLogSeverity.Warning,
                    nameof(RequestMonitoringMiddleware),
                    slow ? "Slow request." : "Request completed with a server error.",
                    HttpMethod: context.Request.Method,
                    Path: context.Request.Path.Value,
                    StatusCode: status,
                    DurationMs: (int)Math.Min(elapsed.TotalMilliseconds, int.MaxValue),
                    Metadata: new Dictionary<string, string?>
                    {
                        ["thresholdMs"] = ((int)_slowThreshold.TotalMilliseconds).ToString()
                    }),
                CancellationToken.None);
        }
    }
}
