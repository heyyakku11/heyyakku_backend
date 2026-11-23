namespace Yakku.Application.Auth.Sms
{
    public interface IOtpSmsQueue
    {
        ValueTask EnqueueAsync(SmsOtpJob job, CancellationToken cancellationToken = default);
    }
}
