using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class SystemLog
    {
        public Guid Id { get; private set; }
        public SystemLogCategory Category { get; private set; }
        public SystemLogSeverity Severity { get; private set; }
        public string Source { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public string? ExceptionType { get; private set; }
        public string? HttpMethod { get; private set; }
        public string? Path { get; private set; }
        public int? StatusCode { get; private set; }
        public int? DurationMs { get; private set; }
        public string? TraceId { get; private set; }
        public string? Metadata { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private SystemLog()
        {
        }

        public SystemLog(
            SystemLogCategory category,
            SystemLogSeverity severity,
            string source,
            string message,
            string? exceptionType,
            string? httpMethod,
            string? path,
            int? statusCode,
            int? durationMs,
            string? traceId,
            string? metadata,
            DateTime? createdAt = null)
        {
            Id = Guid.NewGuid();
            Category = category;
            Severity = severity;
            Source = source;
            Message = message;
            ExceptionType = exceptionType;
            HttpMethod = httpMethod;
            Path = path;
            StatusCode = statusCode;
            DurationMs = durationMs;
            TraceId = traceId;
            Metadata = metadata;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
