using Yakku.Domain.Entities;

namespace Yakku.Application.Polls.Interfaces
{
    public sealed record StoredObjectReference(Guid SessionId, string BucketName, string ObjectKey);

    public sealed record MediaUploadCleanupItem(Guid SessionId, string BucketName, string ObjectKey);

    public sealed record MediaUploadQuotaUsage(
        int PendingCount,
        int IncompleteCount,
        long ReservedBytes);

    public interface IMediaUploadSessionRepository
    {
        Task AddAsync(MediaUploadSession session, CancellationToken cancellationToken = default);

        Task<MediaUploadSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> HasBlockingSelectionAsync(
            MediaOwner owner,
            Guid? pollId,
            long selectionVersion,
            Guid? exceptSessionId,
            bool includeEqual,
            CancellationToken cancellationToken = default);

        Task CancelSupersededAsync(
            MediaOwner owner,
            Guid? pollId,
            long selectionVersion,
            Guid exceptSessionId,
            CancellationToken cancellationToken = default);

        Task<MediaUploadQuotaUsage> GetQuotaUsageAsync(
            MediaOwner owner,
            CancellationToken cancellationToken = default);

        Task<bool> TryMarkUploadedAsync(
            Guid sessionId,
            MediaOwner owner,
            long selectionVersion,
            long actualFileSize,
            long? durationMs,
            CancellationToken cancellationToken = default);

        Task<bool> TryMarkFailedAsync(
            Guid sessionId,
            MediaOwner owner,
            string failureReason,
            CancellationToken cancellationToken = default);

        Task<bool> TryMarkExpiredAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default);

        Task<bool> TryMarkCancelledAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default);

        Task<bool> TryLinkPendingAsync(
            Guid sessionId,
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task<bool> TryMarkAttachedAsync(
            Guid sessionId,
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task<MediaUploadSession?> GetCurrentForPollAsync(
            Guid pollId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<StoredObjectReference>> DetachForDeletedDraftAsync(
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task UnlinkAttachedObjectAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default);

        Task<bool> IsReferencedByPollMediaAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MediaUploadCleanupItem>> ClaimExpiredAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reassigns every guest-owned session (including terminal ones) to the user. Not atomic on its own:
        /// call it inside the guest repository transaction together with the guest delete.
        /// </summary>
        Task<int> ClaimGuestSessionsAsync(
            Guid guestId,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates a guest's live sessions (pending, uploading, uploaded, failed) so media cleanup purges their objects.
        /// </summary>
        Task<int> ExpireGuestSessionsAsync(
            Guid guestId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>Removes the guest's session rows whose stored object is already purged.</summary>
        Task<int> DeletePurgedGuestSessionsAsync(
            Guid guestId,
            CancellationToken cancellationToken = default);

        Task<int> CountGuestSessionsAsync(
            Guid guestId,
            CancellationToken cancellationToken = default);

        Task MarkPurgedAsync(Guid sessionId, CancellationToken cancellationToken = default);

        Task RecordFailureAsync(
            Guid sessionId,
            string failureReason,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
