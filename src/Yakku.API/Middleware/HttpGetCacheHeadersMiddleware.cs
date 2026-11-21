using System.Security.Cryptography;
using System.Text;

namespace Yakku.API.Middleware
{
    /// <summary>
    /// Adds Cache-Control / ETag for safe GET API responses. Honors If-None-Match → 304.
    /// Never applied to non-GET, /hubs, /api/v1/admin, or auth/payment write surfaces.
    /// </summary>
    internal sealed class HttpGetCacheHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public HttpGetCacheHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!HttpMethods.IsGet(context.Request.Method)
                || context.Request.Path.StartsWithSegments("/hubs")
                || context.Request.Path.StartsWithSegments("/api/v1/admin")
                || IsNoStorePath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var originalBody = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);

                if (context.Response.StatusCode is < 200 or >= 300)
                {
                    buffer.Position = 0;
                    await buffer.CopyToAsync(originalBody);
                    return;
                }

                buffer.Position = 0;
                var bodyBytes = buffer.ToArray();
                var etag = "W/\"" + Convert.ToHexString(SHA256.HashData(bodyBytes))[..16] + "\"";
                context.Response.Headers.ETag = etag;

                ApplyCacheControl(context);

                var ifNoneMatch = context.Request.Headers.IfNoneMatch.ToString();
                if (!string.IsNullOrWhiteSpace(ifNoneMatch)
                    && ifNoneMatch.Split(',').Select(value => value.Trim()).Contains(etag, StringComparer.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentLength = 0;
                    context.Response.Body = originalBody;
                    return;
                }

                context.Response.Body = originalBody;
                await originalBody.WriteAsync(bodyBytes);
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }

        private static void ApplyCacheControl(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var authenticated = context.User?.Identity?.IsAuthenticated == true;

            if (IsPublicCatalog(path) && !authenticated)
            {
                context.Response.Headers.CacheControl = "public, max-age=60, stale-while-revalidate=240";
                return;
            }

            if (IsPublicCatalog(path))
            {
                context.Response.Headers.CacheControl = "private, max-age=30, stale-while-revalidate=120";
                return;
            }

            // Authenticated lists / me / unread: revalidate with ETag.
            context.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        }

        private static bool IsPublicCatalog(string path) =>
            path.Contains("/plans", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/check-themes", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/personality/traits", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/checks", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/fun-cards", StringComparison.OrdinalIgnoreCase);

        private static bool IsNoStorePath(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Contains("/auth/", StringComparison.OrdinalIgnoreCase)
                || value.Contains("/payments/", StringComparison.OrdinalIgnoreCase)
                || value.Contains("/votes", StringComparison.OrdinalIgnoreCase)
                || value.Contains("/devices/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
