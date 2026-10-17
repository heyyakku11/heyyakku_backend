using Yakku.Application.Auth.Models;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IEmailSender
    {
        Task<EmailSendResult> SendOtpAsync(
            string email,
            string otp,
            CancellationToken cancellationToken = default);

        /// <summary>Sends a published provider template with the given variables.</summary>
        Task<EmailSendResult> SendTemplateAsync(
            string email,
            string templateId,
            string subject,
            IReadOnlyDictionary<string, string> variables,
            CancellationToken cancellationToken = default);
    }
}
