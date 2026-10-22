using Yakku.Application.Polls;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Storage;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

internal sealed class FakeBuckets : IPollMediaBucketResolver
{
    public string Resolve(PollMediaType mediaType)
    {
        return mediaType.ToString().ToLowerInvariant() + "-bucket";
    }
}

internal sealed class FakeObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, Stored> _objects = new(StringComparer.Ordinal);

    public List<string> Deleted { get; } = [];

    public int HeadCalls { get; private set; }

    public void Put(string objectKey, byte[] bytes, string contentType)
    {
        _objects[objectKey] = new Stored(bytes, contentType);
    }

    public Task<PresignedUpload> CreateUploadUrlAsync(
        string bucketName,
        string objectKey,
        string contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PresignedUpload(
            "PUT",
            "https://storage.test/upload",
            new Dictionary<string, string> { ["Content-Type"] = contentType },
            DateTime.UtcNow.Add(lifetime)));
    }

    public Task<PresignedDownload> CreateDownloadUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PresignedDownload("https://storage.test/download", DateTime.UtcNow.Add(lifetime)));
    }

    public Task<StoredObjectMetadata?> HeadAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        HeadCalls++;
        if (!_objects.TryGetValue(objectKey, out var stored))
        {
            return Task.FromResult<StoredObjectMetadata?>(null);
        }

        return Task.FromResult<StoredObjectMetadata?>(new StoredObjectMetadata(stored.Bytes.Length, stored.ContentType));
    }

    public Task<byte[]> ReadPrefixAsync(
        string bucketName,
        string objectKey,
        int length,
        CancellationToken cancellationToken = default)
    {
        var stored = _objects[objectKey];
        return Task.FromResult(stored.Bytes.Take(length).ToArray());
    }

    public Task<byte[]> ReadObjectAsync(
        string bucketName,
        string objectKey,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        var stored = _objects[objectKey];
        if (stored.Bytes.LongLength > maxBytes)
        {
            throw new InvalidOperationException("Stored object exceeds the configured maximum size.");
        }

        return Task.FromResult(stored.Bytes);
    }

    public bool FailDeletes { get; set; }

    public Task DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        if (FailDeletes)
        {
            throw new InvalidOperationException("Storage is unavailable.");
        }

        Deleted.Add(objectKey);
        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }

    private sealed record Stored(byte[] Bytes, string ContentType);
}

internal sealed class InMemoryPollRepository : IPollRepository
{
    private readonly List<Poll> _polls = [];
    private readonly List<PollVote> _votes;

    public InMemoryPollRepository(List<PollVote> votes)
    {
        _votes = votes;
    }

    public IReadOnlyList<Poll> Polls => _polls;

    public Task AddAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        _polls.Add(poll);
        return Task.CompletedTask;
    }

    public void Delete(Poll poll)
    {
        _polls.Remove(poll);
    }

    public Task<Poll?> GetByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_polls.FirstOrDefault(poll => poll.ShareCode == shareCode));
    }

    public Task<Poll?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_polls.FirstOrDefault(poll => poll.Id == id));
    }

    public Task<Poll?> GetByIdAndCreatorAsync(Guid id, Guid creatorId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_polls.FirstOrDefault(poll => poll.Id == id && poll.CreatorId == creatorId));
    }

    public Task<IReadOnlyList<Poll>> GetCreatedByUserAsync(
        Guid userId,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Poll>>([]);
    }

    public Task<IReadOnlyList<Poll>> GetVisiblePollsAsync(
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int take,
        PollContentType? contentType,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Poll> query = _polls.Where(poll =>
            poll.Status == PollStatus.Active && poll.Visibility == PollVisibility.Public);
        if (contentType is PollContentType selected)
        {
            query = query.Where(poll => poll.ContentType == selected);
        }

        return Task.FromResult<IReadOnlyList<Poll>>(query.Take(take).ToList());
    }

    public Task<int> TryMarkActiveAsync(Guid pollId, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var poll = _polls.FirstOrDefault(item => item.Id == pollId && item.CreatorId == creatorId && item.Status == PollStatus.Draft);
        if (poll is null)
        {
            return Task.FromResult(0);
        }

        poll.MarkPublished();
        return Task.FromResult(1);
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        var snapshot = _polls.ToList();
        try
        {
            await action(cancellationToken);
        }
        catch
        {
            _polls.Clear();
            _polls.AddRange(snapshot);
            throw;
        }
    }

    public Task<IReadOnlyList<Poll>> ListByStatusAsync(PollStatus? status, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Poll>>([]);
    }

    public Task<IReadOnlyList<AnsweredPollEntry>> GetAnsweredByUserAsync(
        Guid userId,
        DateTime? cursorVotedAt,
        Guid? cursorPollId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AnsweredPollEntry>>([]);
    }

    public Task<IReadOnlyDictionary<Guid, PollVoteCountSnapshot>> GetVoteCountsAsync(
        IReadOnlyCollection<Guid> pollIds,
        CancellationToken cancellationToken = default)
    {
        var rows = _votes
            .Where(vote => pollIds.Contains(vote.PollId))
            .GroupBy(vote => new { vote.PollId, vote.PollOptionId })
            .Select(group => new PollVoteCountRow(
                group.Key.PollId,
                group.Key.PollOptionId,
                group.Count(),
                group.Count(vote => vote.Reason != null)));
        return Task.FromResult(PollVoteCountSnapshot.Combine(rows));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class InMemorySessionRepository : IMediaUploadSessionRepository
{
    private static readonly MediaUploadStatus[] Inactive =
    [
        MediaUploadStatus.Cancelled,
        MediaUploadStatus.Failed,
        MediaUploadStatus.Expired
    ];

    private readonly List<MediaUploadSession> _sessions = [];

    public bool ThrowOnUpload { get; set; }

    public Func<string, string, bool>? IsReferenced { get; set; }

    public Task AddAsync(MediaUploadSession session, CancellationToken cancellationToken = default)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<MediaUploadSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.FirstOrDefault(session => session.Id == id));
    }

    public Task<bool> HasBlockingSelectionAsync(
        MediaOwner owner,
        Guid? pollId,
        long selectionVersion,
        Guid? exceptSessionId,
        bool includeEqual,
        CancellationToken cancellationToken = default)
    {
        var match = Owned(owner).Any(session =>
            session.PollId == pollId
            && session.Id != exceptSessionId
            && !Inactive.Contains(session.Status)
            && (includeEqual
                ? session.SelectionVersion >= selectionVersion
                : session.SelectionVersion > selectionVersion));
        return Task.FromResult(match);
    }

    public Task CancelSupersededAsync(
        MediaOwner owner,
        Guid? pollId,
        long selectionVersion,
        Guid exceptSessionId,
        CancellationToken cancellationToken = default)
    {
        foreach (var session in Owned(owner).Where(session =>
                     session.PollId == pollId
                     && session.Id != exceptSessionId
                     && session.SelectionVersion < selectionVersion
                     && session.Status is not MediaUploadStatus.Attached
                         and not MediaUploadStatus.Cancelled
                         and not MediaUploadStatus.Failed
                         and not MediaUploadStatus.Expired))
        {
            session.MarkCancelled();
        }

        return Task.CompletedTask;
    }

    public Task<MediaUploadQuotaUsage> GetQuotaUsageAsync(
        MediaOwner owner,
        CancellationToken cancellationToken = default)
    {
        var active = Owned(owner)
            .Where(session =>
                session.Status is MediaUploadStatus.Pending
                    or MediaUploadStatus.Uploading
                    or MediaUploadStatus.Uploaded
                    or MediaUploadStatus.Attached)
            .ToList();
        return Task.FromResult(new MediaUploadQuotaUsage(
            active.Count(session => session.Status == MediaUploadStatus.Pending),
            active.Count(session => session.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading),
            active.Sum(session => session.ActualFileSize ?? session.ExpectedFileSize ?? 0L)));
    }

    public Task<bool> TryMarkUploadedAsync(
        Guid sessionId,
        MediaOwner owner,
        long selectionVersion,
        long actualFileSize,
        long? durationMs,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnUpload)
        {
            throw new InvalidOperationException("database update failed");
        }

        var session = Find(sessionId, owner);
        if (session is null
            || session.SelectionVersion != selectionVersion
            || session.Status is not (MediaUploadStatus.Pending or MediaUploadStatus.Uploading)
            || HasNewer(session))
        {
            return Task.FromResult(false);
        }

        session.MarkUploaded(actualFileSize, durationMs);
        return Task.FromResult(true);
    }

    public Task<bool> TryMarkFailedAsync(
        Guid sessionId,
        MediaOwner owner,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var session = Find(sessionId, owner);
        if (session is null || session.Status is not (MediaUploadStatus.Pending or MediaUploadStatus.Uploading))
        {
            return Task.FromResult(false);
        }

        session.MarkFailed(failureReason);
        return Task.FromResult(true);
    }

    public Task<bool> TryMarkExpiredAsync(
        Guid sessionId,
        MediaOwner owner,
        CancellationToken cancellationToken = default)
    {
        var session = Find(sessionId, owner);
        if (session is null || session.Status is not (MediaUploadStatus.Pending or MediaUploadStatus.Uploading))
        {
            return Task.FromResult(false);
        }

        session.MarkExpired();
        return Task.FromResult(true);
    }

    public Task<bool> TryMarkCancelledAsync(
        Guid sessionId,
        MediaOwner owner,
        CancellationToken cancellationToken = default)
    {
        var session = Find(sessionId, owner);
        if (session?.Status is not (MediaUploadStatus.Pending or MediaUploadStatus.Uploading or MediaUploadStatus.Uploaded or MediaUploadStatus.Failed))
        {
            return Task.FromResult(false);
        }

        session.MarkCancelled();
        return Task.FromResult(true);
    }

    public Task<bool> TryLinkPendingAsync(Guid sessionId, Guid userId, Guid pollId, CancellationToken cancellationToken = default)
    {
        var session = Find(sessionId, MediaOwner.ForUser(userId));
        if (session is null
            || session.Status is not (MediaUploadStatus.Pending or MediaUploadStatus.Uploading)
            || (session.PollId is not null && session.PollId != pollId)
            || HasNewer(session))
        {
            return Task.FromResult(false);
        }

        session.LinkToPoll(pollId);
        return Task.FromResult(true);
    }

    public Task<bool> TryMarkAttachedAsync(Guid sessionId, Guid userId, Guid pollId, CancellationToken cancellationToken = default)
    {
        var session = Find(sessionId, MediaOwner.ForUser(userId));
        if (session is null
            || session.Status != MediaUploadStatus.Uploaded
            || (session.PollId is not null && session.PollId != pollId)
            || HasNewer(session))
        {
            return Task.FromResult(false);
        }

        session.MarkAttached(pollId);
        return Task.FromResult(true);
    }

    public Task<MediaUploadSession?> GetCurrentForPollAsync(Guid pollId, Guid userId, CancellationToken cancellationToken = default)
    {
        var current = Owned(MediaOwner.ForUser(userId))
            .Where(session => session.PollId == pollId && !Inactive.Contains(session.Status))
            .OrderByDescending(session => session.SelectionVersion)
            .FirstOrDefault();
        return Task.FromResult(current);
    }

    public Task<IReadOnlyList<StoredObjectReference>> DetachForDeletedDraftAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var rows = _sessions
            .Where(session => session.PollId == pollId)
            .Select(session => new StoredObjectReference(session.Id, session.BucketName, session.ObjectKey))
            .ToList();
        foreach (var session in _sessions.Where(session => session.PollId == pollId))
        {
            session.MarkCancelled();
            session.Unlink();
        }

        return Task.FromResult<IReadOnlyList<StoredObjectReference>>(rows);
    }

    public Task UnlinkAttachedObjectAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        foreach (var session in _sessions.Where(session =>
                     session.BucketName == bucketName
                     && session.ObjectKey == objectKey
                     && session.Status == MediaUploadStatus.Attached))
        {
            session.MarkCancelled();
            session.Unlink();
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsReferencedByPollMediaAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsReferenced?.Invoke(bucketName, objectKey) ?? false);
    }

    public Task<IReadOnlyList<MediaUploadCleanupItem>> ClaimExpiredAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var claimed = new List<MediaUploadCleanupItem>();
        foreach (var session in _sessions
                     .Where(session =>
                         session.PurgedAt == null
                         && session.Status != MediaUploadStatus.Attached
                         && (session.ExpiresAt <= utcNow || session.Status == MediaUploadStatus.Failed))
                     .OrderBy(session => session.ExpiresAt)
                     .Take(batchSize))
        {
            if (session.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading or MediaUploadStatus.Uploaded or MediaUploadStatus.Failed)
            {
                session.MarkExpired();
            }

            claimed.Add(new MediaUploadCleanupItem(session.Id, session.BucketName, session.ObjectKey));
        }

        return Task.FromResult<IReadOnlyList<MediaUploadCleanupItem>>(claimed);
    }

    public Task<int> ClaimGuestSessionsAsync(
        Guid guestId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var session in _sessions.Where(session => session.GuestId == guestId).ToList())
        {
            session.ClaimToUser(userId);
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<int> ExpireGuestSessionsAsync(Guid guestId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var session in _sessions.Where(session =>
                     session.GuestId == guestId
                     && session.PurgedAt == null
                     && session.Status is MediaUploadStatus.Pending
                         or MediaUploadStatus.Uploading
                         or MediaUploadStatus.Uploaded
                         or MediaUploadStatus.Failed))
        {
            session.ExpireNow(utcNow);
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<int> DeletePurgedGuestSessionsAsync(Guid guestId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.RemoveAll(session => session.GuestId == guestId && session.PurgedAt != null));
    }

    public Task<int> CountGuestSessionsAsync(Guid guestId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.Count(session => session.GuestId == guestId));
    }

    public Task MarkPurgedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.FirstOrDefault(session => session.Id == sessionId)?.MarkPurged();
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(Guid sessionId, string failureReason, CancellationToken cancellationToken = default)
    {
        _sessions.FirstOrDefault(session => session.Id == sessionId)?.RecordFailure(failureReason);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private IEnumerable<MediaUploadSession> Owned(MediaOwner owner)
    {
        return _sessions.Where(owner.Owns);
    }

    private MediaUploadSession? Find(Guid sessionId, MediaOwner owner)
    {
        return _sessions.FirstOrDefault(session => session.Id == sessionId && owner.Owns(session));
    }

    private bool HasNewer(MediaUploadSession session)
    {
        return _sessions.Any(other =>
            other.UserId == session.UserId
            && other.GuestId == session.GuestId
            && other.PollId == session.PollId
            && other.Id != session.Id
            && other.SelectionVersion > session.SelectionVersion
            && !Inactive.Contains(other.Status));
    }
}

internal sealed class InMemoryVoteRepository : Yakku.Application.Votes.Interfaces.IVoteRepository
{
    private readonly List<PollVote> _votes;

    public InMemoryVoteRepository(List<PollVote> votes)
    {
        _votes = votes;
    }

    public Task AddAsync(PollVote vote, CancellationToken cancellationToken = default)
    {
        _votes.Add(vote);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsForUserAsync(Guid userId, Guid pollId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_votes.Any(vote => vote.VoterId == userId && vote.PollId == pollId));
    }

    public Task<PollVote?> GetByUserAndPollAsync(Guid userId, Guid pollId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_votes.FirstOrDefault(vote => vote.VoterId == userId && vote.PollId == pollId));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var duplicate = _votes
            .GroupBy(vote => (vote.PollId, vote.VoterId))
            .Any(group => group.Count() > 1);
        if (duplicate)
        {
            var extra = _votes.Last();
            _votes.Remove(extra);
            throw Yakku.Application.Votes.VoteExceptions.AlreadyVoted();
        }

        return Task.CompletedTask;
    }
}
