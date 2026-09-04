namespace Yakku.Application.Auth.Interfaces
{
    public interface IEmailSender
    {
        Task SendOtpAsync(string email, string otp, CancellationToken cancellationToken = default);
    }
}
