namespace Yakku.API.AdminSecurity
{
    /// <summary>
    /// Browser hardening headers. The API serves JSON only, so the CSP is the strictest possible: nothing may be
    /// loaded or framed from an API response. Swagger UI (development only) needs scripts, so it is exempt there.
    /// </summary>
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly bool _isDevelopment;

        public SecurityHeadersMiddleware(RequestDelegate next, bool isDevelopment)
        {
            _next = next;
            _isDevelopment = isDevelopment;
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                // Admin responses carry session and CSRF material: never cache them.
                if (context.Request.Path.StartsWithSegments("/api/v1/admin"))
                {
                    headers["Cache-Control"] = "no-store";
                }

                if (!_isDevelopment)
                {
                    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }

                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}
