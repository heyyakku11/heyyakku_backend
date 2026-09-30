namespace Yakku.Application.AdminAuth.DTOs
{
    public class AdminLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
