namespace Yakku.Application.System.DTOs
{
    public class DecryptOtpResponse
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string OtpHash { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public int? AttemptCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? ExpiresInSeconds { get; set; }
    }
}
