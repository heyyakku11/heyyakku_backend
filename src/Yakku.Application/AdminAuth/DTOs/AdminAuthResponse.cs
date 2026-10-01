using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.AdminAuth.DTOs
{
    public class AdminAuthResponse : TokenResponse
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
