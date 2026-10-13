using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Storage;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Services
{
    public class MediaUploadSessionService : IMediaUploadSessionService
    {
        private readonly IMediaUploadSessionRepository _sessions;
        private readonly IPollRepository _polls;
        private readonly IPollMediaBucketResolver _buckets;
        private readonly IObjectStorage _storage;
        private readonly IMediaUploadPolicy _policy;
        private readonly IMediaInspector _inspector;
        private readonly IValidator<CreateMediaUploadSessionRequest> _createValidator;
        private readonly IValidator<CompleteMediaUploadRequest> _completeValidator;

        public MediaUploadSessionService(
            IMediaUploadSessionRepository sessions,
            IPollRepository polls,
            IPollMediaBucketResolver buckets,
            IObjectStorage storage,
            IMediaUploadPolicy policy,
            IMediaInspector inspector,
            IValidator<CreateMediaUploadSessionRequest> createValidator,
            IValidator<CompleteMediaUploadRequest> completeValidator)
        {
            _sessions = sessions;
            _polls = polls;
            _buckets = buckets;
            _storage = storage;
            _policy = policy;
            _inspector = inspector;
            _createValidator = createValidator;
            _completeValidator = completeValidator;
        }

        public async Task<MediaUploadSessionResponse> CreateAsync(
            CreateMediaUploadSessionRequest request,
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
            EnsureOwner(owner);

            var mediaType = Enum.Parse<PollMediaType>(request.MediaType.Trim(), ignoreCase: true);
            if (!_policy.TryNormalizeMime(mediaType, request.ContentType, out var mimeType))
            {
                throw Unsupported(mediaType);
            }

            if (request.FileSize is long fileSize && fileSize > _policy.MaxBytes(mediaType))
            {
                throw TooLarge(mediaType);
            }

            await EnsureQuotaAsync(owner, request.FileSize ?? 0, cancellationToken);

            Guid? pollId = null;
            if (request.PollId is Guid requestedPollId)
            {
                if (owner.IsGuest)
                {
                    throw Conflict("Guests cannot attach uploads to an existing poll.");
                }

                var poll = await _polls.GetByIdAndCreatorAsync(requestedPollId, owner.UserId!.Value, cancellationToken);
                if (poll is null)
                {
                    throw NotFound("Poll not found");
                }

                if (poll.Status != PollStatus.Draft)
                {
                    throw Conflict("Media can only be attached to a draft poll.");
                }

                if (!poll.MatchesMedia(mediaType))
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Media type does not match the poll content type.");
                }

                pollId = requestedPollId;
            }

            var extension = _policy.ExtensionFor(mimeType);
            var bucket = _buckets.Resolve(mediaType);
            var expiresAt = DateTime.UtcNow.Add(_policy.SessionLifetime);
            var session = owner.IsUser
                ? MediaUploadSession.ForUser(
                    owner.UserId!.Value,
                    pollId,
                    mediaType,
                    bucket,
                    extension,
                    mimeType,
                    request.FileSize,
                    request.SelectionVersion,
                    expiresAt)
                : MediaUploadSession.ForGuest(
                    owner.GuestId!.Value,
                    mediaType,
                    bucket,
                    extension,
                    mimeType,
                    request.FileSize,
                    request.SelectionVersion,
                    expiresAt);

            var upload = await _storage.CreateUploadUrlAsync(
                session.BucketName,
                session.ObjectKey,
                session.MimeType,
                _policy.UploadUrlLifetime,
                cancellationToken);

            await _polls.ExecuteInTransactionAsync(async transactionToken =>
            {
                if (await _sessions.HasBlockingSelectionAsync(
                        owner,
                        pollId,
                        request.SelectionVersion,
                        session.Id,
                        includeEqual: true,
                        transactionToken))
                {
                    throw Conflict("A newer media selection already exists.");
                }

                await _sessions.CancelSupersededAsync(
                    owner,
                    pollId,
                    request.SelectionVersion,
                    session.Id,
                    transactionToken);
                await _sessions.AddAsync(session, transactionToken);
                await _sessions.SaveChangesAsync(transactionToken);
            }, cancellationToken);

            return ToResponse(session, upload);
        }

        public async Task<MediaUploadSessionResponse> CompleteAsync(
            Guid sessionId,
            MediaOwner owner,
            CompleteMediaUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            await _completeValidator.ValidateAndThrowAsync(request, cancellationToken);

            var session = await GetOwnedSessionAsync(sessionId, owner, cancellationToken);
            if (request.SelectionVersion != session.SelectionVersion)
            {
                throw Conflict("This upload belongs to an older media selection.");
            }

            if (session.Status == MediaUploadStatus.Attached)
            {
                return ToResponse(session, upload: null);
            }

            var superseded = await _sessions.HasBlockingSelectionAsync(
                owner,
                session.PollId,
                session.SelectionVersion,
                session.Id,
                includeEqual: false,
                cancellationToken);

            if (session.Status == MediaUploadStatus.Uploaded)
            {
                if (superseded)
                {
                    throw Conflict("This upload belongs to an older media selection.");
                }

                return ToResponse(session, upload: null);
            }

            if (session.Status is MediaUploadStatus.Cancelled or MediaUploadStatus.Expired or MediaUploadStatus.Failed)
            {
                throw Conflict("This upload can no longer be completed.");
            }

            if (session.ExpiresAt <= DateTime.UtcNow)
            {
                await _sessions.TryMarkExpiredAsync(session.Id, owner, cancellationToken);
                throw Conflict("This upload session has expired.");
            }

            if (superseded)
            {
                await RejectSupersededAsync(session, owner, cancellationToken);
                throw Conflict("This upload belongs to an older media selection.");
            }

            StoredObjectMetadata? metadata;
            try
            {
                metadata = await _storage.HeadAsync(session.BucketName, session.ObjectKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not AppException and not OperationCanceledException)
            {
                throw Conflict("The uploaded object could not be verified.");
            }

            if (metadata is null)
            {
                throw Conflict("The uploaded object was not found.");
            }

            if (metadata.Size <= 0 || metadata.Size > _policy.MaxBytes(session.MediaType))
            {
                await FailAndPurgeAsync(session, owner, "The uploaded file exceeds the allowed size.", cancellationToken);
                throw TooLarge(session.MediaType);
            }

            if (session.ExpectedFileSize is long expected && metadata.Size != expected)
            {
                await FailAndPurgeAsync(session, owner, "The uploaded file size does not match the original selection.", cancellationToken);
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "The uploaded file size does not match the original selection.");
            }

            var observedType = PollMediaCatalog.Normalize(metadata.ContentType);
            if (!IsGenericContentType(observedType)
                && !_policy.TryNormalizeMime(session.MediaType, observedType, out _))
            {
                await FailAndPurgeAsync(session, owner, "The uploaded file type is not allowed.", cancellationToken);
                throw Unsupported(session.MediaType);
            }

            byte[] prefix;
            try
            {
                prefix = await _storage.ReadPrefixAsync(session.BucketName, session.ObjectKey, 32, cancellationToken);
            }
            catch (Exception exception) when (exception is not AppException and not OperationCanceledException)
            {
                throw Conflict("The uploaded object could not be verified.");
            }

            if (!MediaSignature.Matches(session.MediaType, session.MimeType, prefix))
            {
                await FailAndPurgeAsync(session, owner, "The uploaded file content does not match the declared media type.", cancellationToken);
                throw Unsupported(session.MediaType);
            }

            long? durationMs = null;
            if (session.MediaType is PollMediaType.Audio or PollMediaType.Video)
            {
                byte[] content;
                try
                {
                    content = await _storage.ReadObjectAsync(
                        session.BucketName,
                        session.ObjectKey,
                        _policy.MaxBytes(session.MediaType),
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not AppException and not OperationCanceledException)
                {
                    await FailAndPurgeAsync(session, owner, "The uploaded file could not be inspected.", cancellationToken);
                    throw Conflict("The uploaded object could not be verified.");
                }

                MediaInspectionResult inspection;
                try
                {
                    inspection = _inspector.Inspect(session.MediaType, session.MimeType, content);
                }
                catch (Exception exception) when (exception is not AppException and not OperationCanceledException)
                {
                    await FailAndPurgeAsync(session, owner, "The uploaded file could not be inspected.", cancellationToken);
                    throw Unsupported(session.MediaType);
                }

                durationMs = inspection.DurationMs;
                var maxDuration = _policy.MaxDuration(session.MediaType);
                if (durationMs is null || maxDuration is null || TimeSpan.FromMilliseconds(durationMs.Value) > maxDuration)
                {
                    await FailAndPurgeAsync(session, owner, "The uploaded media exceeds the allowed duration.", cancellationToken);
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        $"The media exceeds the {maxDuration?.TotalSeconds:0} second duration limit.");
                }
            }

            var marked = await _sessions.TryMarkUploadedAsync(
                session.Id,
                owner,
                session.SelectionVersion,
                metadata.Size,
                durationMs,
                cancellationToken);

            var current = await _sessions.GetByIdAsync(session.Id, cancellationToken) ?? session;
            if (!marked)
            {
                if (current.Status == MediaUploadStatus.Uploaded)
                {
                    return ToResponse(current, upload: null);
                }

                throw Conflict("This upload can no longer be completed.");
            }

            return ToResponse(current, upload: null);
        }

        public async Task<MediaUploadSessionResponse> GetAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            var session = await GetOwnedSessionAsync(sessionId, owner, cancellationToken);
            return ToResponse(session, upload: null);
        }

        public async Task<MediaAccessResponse> GetAccessAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            var session = await GetOwnedSessionAsync(sessionId, owner, cancellationToken);
            if (session.Status is not (MediaUploadStatus.Uploaded or MediaUploadStatus.Attached))
            {
                throw Conflict("This media is not available for download.");
            }

            var download = await _storage.CreateDownloadUrlAsync(
                session.BucketName,
                session.ObjectKey,
                _policy.DownloadUrlLifetime,
                cancellationToken);

            return new MediaAccessResponse
            {
                SessionId = session.Id,
                Url = download.Url,
                ExpiresAt = download.ExpiresAt
            };
        }

        public async Task CancelAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default)
        {
            var session = await GetOwnedSessionAsync(sessionId, owner, cancellationToken);
            if (session.Status == MediaUploadStatus.Cancelled)
            {
                return;
            }

            if (session.Status == MediaUploadStatus.Attached
                || await _sessions.IsReferencedByPollMediaAsync(session.BucketName, session.ObjectKey, cancellationToken))
            {
                throw Conflict("Attached media cannot be cancelled.");
            }

            if (session.Status != MediaUploadStatus.Expired)
            {
                await _sessions.TryMarkCancelledAsync(session.Id, owner, cancellationToken);
            }

            if (await _sessions.IsReferencedByPollMediaAsync(session.BucketName, session.ObjectKey, cancellationToken))
            {
                return;
            }

            await DeleteObjectBestEffortAsync(session.Id, session.BucketName, session.ObjectKey, cancellationToken);
        }

        private async Task EnsureQuotaAsync(MediaOwner owner, long incomingBytes, CancellationToken cancellationToken)
        {
            var usage = await _sessions.GetQuotaUsageAsync(owner, cancellationToken);
            if (usage.PendingCount >= _policy.MaxPendingPerUser)
            {
                throw Conflict("Pending upload quota exceeded.");
            }

            if (usage.IncompleteCount >= _policy.MaxConcurrentIncompletePerUser)
            {
                throw Conflict("Concurrent upload quota exceeded.");
            }

            if (usage.ReservedBytes + incomingBytes > _policy.MaxStorageBytesPerUser)
            {
                throw Conflict("Storage quota exceeded.");
            }
        }

        private async Task RejectSupersededAsync(
            MediaUploadSession session,
            MediaOwner owner,
            CancellationToken cancellationToken)
        {
            await _sessions.TryMarkCancelledAsync(session.Id, owner, cancellationToken);
            if (!await _sessions.IsReferencedByPollMediaAsync(session.BucketName, session.ObjectKey, cancellationToken))
            {
                await DeleteObjectBestEffortAsync(session.Id, session.BucketName, session.ObjectKey, cancellationToken);
            }
        }

        private async Task FailAndPurgeAsync(
            MediaUploadSession session,
            MediaOwner owner,
            string reason,
            CancellationToken cancellationToken)
        {
            await _sessions.TryMarkFailedAsync(session.Id, owner, MediaFailure.Sanitize(reason), cancellationToken);
            if (!await _sessions.IsReferencedByPollMediaAsync(session.BucketName, session.ObjectKey, cancellationToken))
            {
                await DeleteObjectBestEffortAsync(session.Id, session.BucketName, session.ObjectKey, cancellationToken);
            }
        }

        private async Task DeleteObjectBestEffortAsync(
            Guid sessionId,
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken)
        {
            try
            {
                await _storage.DeleteAsync(bucketName, objectKey, cancellationToken);
                await _sessions.MarkPurgedAsync(sessionId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await _sessions.RecordFailureAsync(
                    sessionId,
                    MediaFailure.Sanitize("Storage delete failed."),
                    cancellationToken);
            }
        }

        private async Task<MediaUploadSession> GetOwnedSessionAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken)
        {
            EnsureOwner(owner);
            var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is null || !owner.Owns(session))
            {
                throw NotFound("Upload session not found");
            }

            return session;
        }

        private static void EnsureOwner(MediaOwner owner)
        {
            if (!owner.IsUser && !owner.IsGuest)
            {
                throw new AppException(401, ApiErrorCodes.Unauthorized, "Authentication is required.");
            }
        }

        private static bool IsGenericContentType(string contentType)
        {
            return contentType.Length == 0
                   || contentType is "application/octet-stream" or "binary/octet-stream";
        }

        private static MediaUploadSessionResponse ToResponse(MediaUploadSession session, PresignedUpload? upload)
        {
            return new MediaUploadSessionResponse
            {
                SessionId = session.Id,
                Status = session.Status.ToString().ToLowerInvariant(),
                MediaType = session.MediaType.ToString().ToLowerInvariant(),
                MimeType = session.MimeType,
                ObjectKey = session.ObjectKey,
                ExpectedFileSize = session.ExpectedFileSize,
                ActualFileSize = session.ActualFileSize,
                DurationMs = session.DurationMs,
                SelectionVersion = session.SelectionVersion,
                ExpiresAt = session.ExpiresAt,
                FailureReason = session.FailureReason,
                Upload = upload is null
                    ? null
                    : new MediaUploadTargetResponse
                    {
                        Method = upload.Method,
                        Url = upload.Url,
                        Headers = upload.Headers.ToDictionary(pair => pair.Key, pair => pair.Value)
                    }
            };
        }

        private static AppException NotFound(string message)
        {
            return new AppException(404, ApiErrorCodes.NotFound, message);
        }

        private static AppException Conflict(string message)
        {
            return new AppException(409, ApiErrorCodes.Conflict, message);
        }

        private static AppException Unsupported(PollMediaType mediaType)
        {
            return new AppException(
                415,
                ApiErrorCodes.UnsupportedMediaType,
                $"This file is not an allowed {mediaType.ToString().ToLowerInvariant()} type.");
        }

        private AppException TooLarge(PollMediaType mediaType)
        {
            return new AppException(
                413,
                ApiErrorCodes.PayloadTooLarge,
                $"The file is larger than the {_policy.MaxBytes(mediaType)} byte limit.");
        }
    }
}
