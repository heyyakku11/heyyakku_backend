using System.Diagnostics;

namespace Yakku.Application.Audit
{
    /// <summary>
    /// Stamps request start on <c>HttpContext.Items</c> so audit writers can record elapsed ms when an
    /// entry does not supply its own latency (provider round-trips still pass an explicit value).
    /// </summary>
    public static class AuditRequestTiming
    {
        public const string HttpContextItemKey = "Yakku.Audit.RequestStartTimestamp";

        public static void MarkStart(IDictionary<object, object?> items)
        {
            items[HttpContextItemKey] = Stopwatch.GetTimestamp();
        }

        public static int? ElapsedMilliseconds(IDictionary<object, object?>? items)
        {
            if (items is null
                || !items.TryGetValue(HttpContextItemKey, out var raw)
                || raw is not long start)
            {
                return null;
            }

            var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (double.IsNaN(ms) || ms < 0)
            {
                return 0;
            }

            return (int)Math.Min(ms, int.MaxValue);
        }

        public static int? Resolve(int? explicitLatencyMs, IDictionary<object, object?>? items)
        {
            if (explicitLatencyMs is int latency)
            {
                return Math.Max(0, latency);
            }

            return ElapsedMilliseconds(items);
        }
    }
}
