namespace Yakku.Application.Email.Interfaces
{
    public interface IAdminEmailQueue
    {
        ValueTask EnqueueAsync(AdminEmailJob job, CancellationToken cancellationToken = default);
    }
}
