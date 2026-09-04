namespace Yakku.Application.Auth.DTOs
{
    public class RequestOtpResponse
    {
        public string Purpose { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
    }
}
