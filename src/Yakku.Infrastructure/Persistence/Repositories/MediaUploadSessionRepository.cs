using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Yakku.Application.Polls;
using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class MediaUploadSessionRepository : IMediaUploadSessionRepository
    {
        private static readonly MediaUploadStatus[] InactiveStatuses =
        [
            MediaUploadStatus.Cancelled,
            MediaUploadStatus.Failed,
            MediaUploadStatus.Expired
        ];

        private readonly YakkuDbContext _context;

        public MediaUploadSessionRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MediaUploadSession session, CancellationToken cancellationToken = default)
        {
            await _context.MediaUploadSessions.AddAsync(session, cancellationToken);
        }

        public async Task<MediaUploadSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.MediaUploadSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
        }

        public async Task<bool> HasBlockingSelectionAsync(
            MediaOwner owner,
            Guid? pollId,
            long selectionVersion,
            Guid? exceptSessionId,
            bool includeEqual,
            CancellationToken cancellationToken = default)
        {
            var query = OwnedQuery(owner)
                .AsNoTracking()
                .Where(session =>
                    session.PollId == pollId
                    && !InactiveStatuses.Contains(session.Status));

            if (exceptSessionId is Guid exceptId)
            {
                query = query.Where(session => session.Id != exceptId);
            }

            query = includeEqual
                ? query.Where(session => session.SelectionVersion >= selectionVersion)
                : query.Where(session => session.SelectionVersion > selectionVersion);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task CancelSupersededAsync(
            MediaOwner owner,
            Guid? pollId,
            long selectionVersion,
            Guid exceptSessionId,
            CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            await OwnedQuery(owner)
                .Where(session =>
                    session.PollId == pollId
                    && session.Id != exceptSessionId
                    && session.SelectionVersion < selectionVersion
                    && session.Status != MediaUploadStatus.Attached
                    && session.Status != MediaUploadStatus.Cancelled
                    && session.Status != MediaUploadStatus.Failed
                    && session.Status != MediaUploadStatus.Expired)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.Status, MediaUploadStatus.Cancelled)
                        .SetProperty(session => session.ExpiresAt, utcNow)
                        .SetProperty(session => session.UpdatedAt, utcNow),
                    cancellationToken);
        }

        public async Task<MediaUploadQuotaUsage> GetQuotaUsageAsync(
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            var active = await OwnedQuery(owner)
                .AsNoTracking()
                .Where(session =>
                    session.Status == MediaUploadStatus.Pending
                    || session.Status == MediaUploadStatus.Uploading
                    || session.Status == MediaUploadStatus.Uploaded
                    || session.Status == MediaUploadStatus.Attached)
                .Select(session => new
                {
                    session.Status,
                    Size = session.ActualFileSize ?? session.ExpectedFileSize ?? 0L
                })
                .ToListAsync(cancellationToken);

            var pending = active.Count(row => row.Status == MediaUploadStatus.Pending);
            var incomplete = active.Count(row =>
                row.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading);
            var reserved = active.Sum(row => row.Size);
            return new MediaUploadQuotaUsage(pending, incomplete, reserved);
        }

        public Task<bool> TryMarkUploadedAsync(
            Guid sessionId,
            MediaOwner owner,
            long selectionVersion,
            long actualFileSize,
            long? durationMs,
            CancellationToken cancellationToken = default)
        {
            return TransitionAsync(
                sessionId,
                owner,
                selectionVersion,
                requireCurrentSelection: true,
                session => session.Status == MediaUploadStatus.Pending || session.Status == MediaUploadStatus.Uploading,
                setters => setters
                    .SetProperty(session => session.Status, MediaUploadStatus.Uploaded)
                    .SetProperty(session => session.ActualFileSize, actualFileSize)
                    .SetProperty(session => session.DurationMs, durationMs)
                    .SetProperty(session => session.FailureReason, (string?)null)
                    .SetProperty(session => session.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
        }

        public Task<bool> TryMarkFailedAsync(
            Guid sessionId,
            MediaOwner owner,
            string failureReason,
            CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            return TransitionAsync(
                sessionId,
                owner,
                selectionVersion: null,
                requireCurrentSelection: false,
                session => session.Status == MediaUploadStatus.Pending || session.Status == MediaUploadStatus.Uploading,
                setters => setters
                    .SetProperty(session => session.Status, MediaUploadStatus.Failed)
                    .SetProperty(session => session.FailureReason, failureReason)
                    .SetProperty(session => session.ExpiresAt, utcNow)
                    .SetProperty(session => session.UpdatedAt, utcNow),
                cancellationToken);
        }

        public Task<bool> TryMarkExpiredAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            return TransitionAsync(
                sessionId,
                owner,
                selectionVersion: null,
                requireCurrentSelection: false,
                session => session.Status == MediaUploadStatus.Pending || session.Status == MediaUploadStatus.Uploading,
                setters => setters
                    .SetProperty(session => session.Status, MediaUploadStatus.Expired)
                    .SetProperty(session => session.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
        }

        public Task<bool> TryMarkCancelledAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            return TransitionAsync(
                sessionId,
                owner,
                selectionVersion: null,
                requireCurrentSelection: false,
                session => session.Status == MediaUploadStatus.Pending
                    || session.Status == MediaUploadStatus.Uploading
                    || session.Status == MediaUploadStatus.Uploaded
                    || session.Status == MediaUploadStatus.Failed,
                setters => setters
                    .SetProperty(session => session.Status, MediaUploadStatus.Cancelled)
                    .SetProperty(session => session.ExpiresAt, DateTime.UtcNow)
                    .SetProperty(session => session.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
        }

        public Task<bool> TryLinkPendingAsync(
            Guid sessionId,
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            return TransitionAsync(
                sessionId,
                MediaOwner.ForUser(userId),
                selectionVersion: null,
                requireCurrentSelection: true,
                session => (session.Status == MediaUploadStatus.Pending || session.Status == MediaUploadStatus.Uploading)
                    && (session.PollId == null || session.PollId == pollId),
                setters => setters
                    .SetProperty(session => session.PollId, pollId)
                    .SetProperty(session => session.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
        }

        public Task<bool> TryMarkAttachedAsync(
            Guid sessionId,
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            return TransitionAsync(
                sessionId,
                MediaOwner.ForUser(userId),
                selectionVersion: null,
                requireCurrentSelection: true,
                session => session.Status == MediaUploadStatus.Uploaded
                    && (session.PollId == null || session.PollId == pollId),
                setters => setters
                    .SetProperty(session => session.Status, MediaUploadStatus.Attached)
                    .SetProperty(session => session.PollId, pollId)
                    .SetProperty(session => session.AttachedAt, utcNow)
                    .SetProperty(session => session.FailureReason, (string?)null)
                    .SetProperty(session => session.UpdatedAt, utcNow),
                cancellationToken);
        }

        public async Task<MediaUploadSession?> GetCurrentForPollAsync(
            Guid pollId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.MediaUploadSessions
                .AsNoTracking()
                .Where(session =>
                    session.PollId == pollId
                    && session.UserId == userId
                    && !InactiveStatuses.Contains(session.Status))
                .OrderByDescending(session => session.SelectionVersion)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<StoredObjectReference>> DetachForDeletedDraftAsync(
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.MediaUploadSessions
                .AsNoTracking()
                .Where(session => session.PollId == pollId)
                .Select(session => new StoredObjectReference(session.Id, session.BucketName, session.ObjectKey))
                .ToListAsync(cancellationToken);

            var utcNow = DateTime.UtcNow;
            await _context.MediaUploadSessions
                .Where(session => session.PollId == pollId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.PollId, (Guid?)null)
                        .SetProperty(session => session.Status, MediaUploadStatus.Cancelled)
                        .SetProperty(session => session.ExpiresAt, utcNow)
                        .SetProperty(session => session.UpdatedAt, utcNow),
                    cancellationToken);

            return rows;
        }

        public async Task UnlinkAttachedObjectAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            await _context.MediaUploadSessions
                .Where(session =>
                    session.BucketName == bucketName
                    && session.ObjectKey == objectKey
                    && session.Status == MediaUploadStatus.Attached)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.PollId, (Guid?)null)
                        .SetProperty(session => session.Status, MediaUploadStatus.Cancelled)
                        .SetProperty(session => session.ExpiresAt, utcNow)
                        .SetProperty(session => session.UpdatedAt, utcNow),
                    cancellationToken);
        }

        public async Task<bool> IsReferencedByPollMediaAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            return await _context.PollMedia.AnyAsync(
                media => media.BucketName == bucketName && media.ObjectKey == objectKey,
                cancellationToken);
        }

        public async Task<IReadOnlyList<MediaUploadCleanupItem>> ClaimExpiredAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var candidates = await _context.MediaUploadSessions
                .AsNoTracking()
                .Where(session =>
                    session.PurgedAt == null
                    && session.Status != MediaUploadStatus.Attached
                    && (session.ExpiresAt <= utcNow || session.Status == MediaUploadStatus.Failed))
                .OrderBy(session => session.ExpiresAt)
                .Select(session => new
                {
                    session.Id,
                    session.Status,
                    session.BucketName,
                    session.ObjectKey
                })
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            var claimed = new List<MediaUploadCleanupItem>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (candidate.Status is MediaUploadStatus.Pending
                    or MediaUploadStatus.Uploading
                    or MediaUploadStatus.Uploaded
                    or MediaUploadStatus.Failed)
                {
                    var updated = await _context.MediaUploadSessions
                        .Where(session =>
                            session.Id == candidate.Id
                            && session.Status == candidate.Status
                            && session.PurgedAt == null)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(session => session.Status, MediaUploadStatus.Expired)
                                .SetProperty(session => session.UpdatedAt, utcNow),
                            cancellationToken);
                    if (updated != 1)
                    {
                        continue;
                    }
                }

                claimed.Add(new MediaUploadCleanupItem(candidate.Id, candidate.BucketName, candidate.ObjectKey));
            }

            return claimed;
        }

        public async Task<int> ClaimGuestSessionsAsync(
            Guid guestId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Reassign every guest-owned row (including terminal) so the guest FK can be cleared/deleted.
            var utcNow = DateTime.UtcNow;
            return await _context.MediaUploadSessions
                .Where(session => session.GuestId == guestId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.UserId, userId)
                        .SetProperty(session => session.GuestId, (Guid?)null)
                        .SetProperty(session => session.UpdatedAt, utcNow),
                    cancellationToken);
        }

        public async Task MarkPurgedAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            await _context.MediaUploadSessions
                .Where(session => session.Id == sessionId && session.Status != MediaUploadStatus.Attached)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.PurgedAt, utcNow)
                        .SetProperty(session => session.FailureReason, (string?)null)
                        .SetProperty(session => session.UpdatedAt, utcNow),
                    cancellationToken);
        }

        public async Task RecordFailureAsync(
            Guid sessionId,
            string failureReason,
            CancellationToken cancellationToken = default)
        {
            await _context.MediaUploadSessions
                .Where(session => session.Id == sessionId && session.Status != MediaUploadStatus.Attached)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.FailureReason, failureReason)
                        .SetProperty(session => session.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        private IQueryable<MediaUploadSession> OwnedQuery(MediaOwner owner)
        {
            if (owner.UserId is Guid userId)
            {
                return _context.MediaUploadSessions.Where(session => session.UserId == userId);
            }

            if (owner.GuestId is Guid guestId)
            {
                return _context.MediaUploadSessions.Where(session => session.GuestId == guestId);
            }

            return _context.MediaUploadSessions.Where(_ => false);
        }

        private async Task<bool> TransitionAsync(
            Guid sessionId,
            MediaOwner owner,
            long? selectionVersion,
            bool requireCurrentSelection,
            global::System.Linq.Expressions.Expression<Func<MediaUploadSession, bool>> predicate,
            Action<UpdateSettersBuilder<MediaUploadSession>> setProperties,
            CancellationToken cancellationToken)
        {
            var query = OwnedQuery(owner).Where(session => session.Id == sessionId);
            if (selectionVersion is long version)
            {
                query = query.Where(session => session.SelectionVersion == version);
            }

            query = query.Where(predicate);
            if (requireCurrentSelection)
            {
                if (owner.UserId is Guid userId)
                {
                    query = query.Where(session =>
                        !_context.MediaUploadSessions.Any(other =>
                            other.UserId == userId
                            && other.PollId == session.PollId
                            && other.Id != session.Id
                            && other.SelectionVersion > session.SelectionVersion
                            && !InactiveStatuses.Contains(other.Status)));
                }
                else if (owner.GuestId is Guid guestId)
                {
                    query = query.Where(session =>
                        !_context.MediaUploadSessions.Any(other =>
                            other.GuestId == guestId
                            && other.PollId == session.PollId
                            && other.Id != session.Id
                            && other.SelectionVersion > session.SelectionVersion
                            && !InactiveStatuses.Contains(other.Status)));
                }
            }

            var updated = await query.ExecuteUpdateAsync(setProperties, cancellationToken);
            return updated == 1;
        }
    }
}
