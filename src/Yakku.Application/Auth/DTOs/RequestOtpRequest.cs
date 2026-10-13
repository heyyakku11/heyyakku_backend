namespace Yakku.Application.Auth.DTOs
{
    public class RequestOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}
