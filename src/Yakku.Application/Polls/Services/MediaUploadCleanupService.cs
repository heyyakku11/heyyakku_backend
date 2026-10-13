using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Storage;

namespace Yakku.Application.Polls.Services
{
    public class MediaUploadCleanupService : IMediaUploadCleanupService
    {
        private readonly IMediaUploadSessionRepository _sessions;
        private readonly IObjectStorage _storage;

        public MediaUploadCleanupService(
            IMediaUploadSessionRepository sessions,
            IObjectStorage storage)
        {
            _sessions = sessions;
            _storage = storage;
        }

        public async Task CleanupAsync(CancellationToken cancellationToken = default)
        {
            var claimed = await _sessions.ClaimExpiredAsync(DateTime.UtcNow, batchSize: 50, cancellationToken);
            foreach (var item in claimed)
            {
                if (await _sessions.IsReferencedByPollMediaAsync(item.BucketName, item.ObjectKey, cancellationToken))
                {
                    continue;
                }

                try
                {
                    await _storage.DeleteAsync(item.BucketName, item.ObjectKey, cancellationToken);
                    await _sessions.MarkPurgedAsync(item.SessionId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await _sessions.RecordFailureAsync(
                        item.SessionId,
                        MediaFailure.Sanitize("Storage delete failed."),
                        cancellationToken);
                }
            }
        }
    }
}
