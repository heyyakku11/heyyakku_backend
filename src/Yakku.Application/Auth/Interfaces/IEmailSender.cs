using Yakku.Application.Auth.Models;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IEmailSender
    {
        Task<EmailSendResult> SendOtpAsync(
            string email,
            string otp,
            CancellationToken cancellationToken = default);
    }
}
