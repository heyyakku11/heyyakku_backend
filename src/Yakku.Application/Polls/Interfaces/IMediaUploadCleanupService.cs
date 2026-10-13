namespace Yakku.Application.Polls.Interfaces
{
    public interface IMediaUploadCleanupService
    {
        Task CleanupAsync(CancellationToken cancellationToken = default);
    }
}
