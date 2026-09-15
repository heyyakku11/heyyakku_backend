namespace Yakku.Application.Email.Interfaces
{
    public interface IOtpEmailQueue
    {
        ValueTask EnqueueAsync(EmailJob job, CancellationToken cancellationToken = default);
    }
}
