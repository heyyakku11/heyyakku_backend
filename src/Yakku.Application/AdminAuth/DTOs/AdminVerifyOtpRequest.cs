namespace Yakku.Application.AdminAuth.DTOs
{
    public class AdminVerifyOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }
}
