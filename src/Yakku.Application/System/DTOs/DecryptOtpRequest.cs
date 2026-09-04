namespace Yakku.Application.System.DTOs
{
    public class DecryptOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string? OtpHash { get; set; }
    }
}
