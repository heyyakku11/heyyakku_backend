using Yakku.Domain.Enums;

namespace Yakku.Application.System.DTOs
{
    public class SystemLogWriteRequest
    {
        public SystemLogLevel Level { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public object? Details { get; init; }
        public Guid? UserId { get; init; }
        public Guid? GuestId { get; init; }
        public string? Path { get; init; }
    }
}
