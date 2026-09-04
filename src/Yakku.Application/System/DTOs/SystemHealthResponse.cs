using System.Text.Json.Serialization;

namespace Yakku.Application.System.DTOs
{
    public class SystemHealthResponse
    {
        public string Status { get; set; } = "Healthy";
        public DateTime CheckedAt { get; set; }
        public List<SystemHealthCheck> Checks { get; set; } = [];
    }

    public class SystemHealthCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }
    }
}
