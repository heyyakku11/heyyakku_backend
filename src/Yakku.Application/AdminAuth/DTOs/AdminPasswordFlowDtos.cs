namespace Yakku.Application.AdminAuth.DTOs
{
    public class AdminConfirmSecretRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SecretCode { get; set; } = string.Empty;
    }

    public class AdminVerifyEmailRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class AdminForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class AdminResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class AdminChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>Returned by the registration steps so the web app can show a countdown.</summary>
    public class AdminRegistrationStepResponse
    {
        public int ExpiresInSeconds { get; set; }
    }
}
