using Yakku.Application.Auth.Sms;

namespace Yakku.Infrastructure.Sms
{
    /// <summary>Used when <c>Msg91:Enabled</c> is false (e.g. Development appsettings). Sends nothing and reports it honestly; it never pretends to deliver.</summary>
    public sealed class DisabledSmsService : ISmsService
    {
        public bool IsEnabled => false;

        public Task<SmsSendResult> SendOtpAsync(SmsOtpMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SmsSendResult.Failed(SmsFailureCategory.Disabled));
        }
    }
}
