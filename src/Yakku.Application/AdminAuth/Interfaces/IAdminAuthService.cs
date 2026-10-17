using Yakku.Application.AdminAuth.DTOs;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminAuthService
    {
        /// <summary>Step 1: keeps the email and password in a temporary record (15 minutes).</summary>
        Task<AdminRegistrationStepResponse> RegisterAsync(
            AdminRegisterRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Step 2: checks the pending record and the admin secret code, then emails the verification link.</summary>
        Task<AdminRegistrationStepResponse> ConfirmSecretAsync(
            AdminConfirmSecretRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Step 3: the emailed link token creates the admin account.</summary>
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
