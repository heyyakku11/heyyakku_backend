using Yakku.Application.AdminAuth.DTOs;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminAuthService
    {
        /// <summary>Registration step 1: checks the admin secret code, keeps the email and password in a temporary record (15 minutes) and emails the verification link (10 minutes).</summary>
        Task<AdminRegistrationStepResponse> RegisterAsync(
            AdminRegisterRequest request,
            CancellationToken cancellationToken = default);

        Task VerifyEmailAsync(
            AdminVerifyEmailRequest request,
            CancellationToken cancellationToken = default);

        Task<AdminAuthResponse> LoginAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Always succeeds from the caller's view; emails a reset link only when the admin exists.</summary>
        Task ForgotPasswordAsync(
            AdminForgotPasswordRequest request,
            CancellationToken cancellationToken = default);

        Task ResetPasswordAsync(
            AdminResetPasswordRequest request,
            CancellationToken cancellationToken = default);

        Task ChangePasswordAsync(
            Guid adminId,
            AdminChangePasswordRequest request,
            CancellationToken cancellationToken = default);
    }
}
