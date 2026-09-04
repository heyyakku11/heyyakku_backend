using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Models
{
    public class OtpChallenge
    {
        public string OtpHash { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; }
        public string? DisplayName { get; set; }
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
