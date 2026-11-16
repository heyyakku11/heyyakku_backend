using Yakku.Application.Auth.Sms;

namespace Yakku.Infrastructure.Sms
{
    /// <summary>Used when <c>MSG91_ENABLED=false</c>. Sends nothing and reports it honestly; it never pretends to deliver.</summary>
    public sealed class DisabledSmsService : ISmsService
    {
        public bool IsEnabled => false;

        public Task<SmsSendResult> SendOtpAsync(SmsOtpMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SmsSendResult.Failed(SmsFailureCategory.Disabled));
        }
    }
}
