using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Mapper;
using MediaOwner = Yakku.Application.Polls.MediaOwner;
using Yakku.Application.Storage;
using Yakku.Application.Users;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using PollEntity = Yakku.Domain.Entities.Poll;

namespace Yakku.Application.Polls.Services
{
    public class PollService : IPollService
    {
        private readonly IPollRepository _pollRepository;
        private readonly IMediaUploadSessionRepository _sessions;
        private readonly IObjectStorage _storage;
        private readonly IMediaUploadPolicy _policy;
        private readonly IValidator<CreatePollRequest> _createValidator;
        private readonly IValidator<UpdatePollRequest> _updateValidator;
        private readonly IAuditLogWriter _audit;

        public PollService(
            IPollRepository pollRepository,
            IMediaUploadSessionRepository sessions,
            IObjectStorage storage,
            IMediaUploadPolicy policy,
            IValidator<CreatePollRequest> createValidator,
            IValidator<UpdatePollRequest> updateValidator,
            IAuditLogWriter audit)
        {
            _pollRepository = pollRepository;
            _sessions = sessions;
            _storage = storage;
            _policy = policy;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _audit = audit;
        }

        public Task<PollResponse> CreateAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return AuditedAsync(
                AuditEvents.Poll.Created,
                null,
                creatorId,
                null,
                PollStatus.Active,
                () => CreateCoreAsync(request, creatorId, cancellationToken),
                response => response.Id);
        }

        private async Task<PollResponse> CreateCoreAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

            var contentType = Enum.Parse<PollContentType>(request.ContentType.Trim(), ignoreCase: true);
            MediaUploadSession? session = null;
            if (contentType == PollContentType.Question)
            {
                if (request.MediaUploadSessionId is not null)
                {
                    throw Validation("Question polls cannot have media.");
                }
            }
            else
            {
                if (request.MediaUploadSessionId is not Guid sessionId)
                {
                    throw Validation("A completed media upload is required for this poll.");
                }

                session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
                if (session is null || session.UserId != creatorId)
                {
                    throw NotFound("Upload session not found");
                }

                if (!Matches(contentType, session.MediaType))
                {
                    throw Validation("Media type does not match the poll content type.");
                }

                if (session.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading)
                {
                    throw Conflict("Media must be uploaded before creating the poll.");
                }

                if (session.Status is not MediaUploadStatus.Uploaded)
                {
                    throw Conflict("This upload can no longer be attached.");
                }

                if (await _sessions.HasBlockingSelectionAsync(
                        MediaOwner.ForUser(creatorId),
                        session.PollId,
                        session.SelectionVersion,
                        session.Id,
                        includeEqual: false,
                        cancellationToken))
                {
                    throw Conflict("This upload belongs to an older media selection.");
                }
            }

            if (request.ExpiresAt is not null && ToUtc(request.ExpiresAt) <= DateTime.UtcNow)
            {
                throw Validation("Expiry must be in the future.");
            }

            var poll = new PollEntity(
                creatorId,
                request.Question.Trim(),
                contentType,
                ToUtc(request.ExpiresAt));

            var useClientOrder = request.Options.All(option => option.DisplayOrder is > 0);
            for (var i = 0; i < request.Options.Count; i++)
            {
                var order = useClientOrder ? request.Options[i].DisplayOrder!.Value : i + 1;
                poll.AddTextOption(request.Options[i].Text!.Trim(), order);
            }

            await _pollRepository.ExecuteInTransactionAsync(async transactionToken =>
            {
                await _pollRepository.AddAsync(poll, transactionToken);
                await _pollRepository.SaveChangesAsync(transactionToken);

                if (session is not null)
                {
                    var attached = await _sessions.TryMarkAttachedAsync(
                        session.Id,
                        creatorId,
                        poll.Id,
                        transactionToken);
                    if (!attached)
                    {
                        throw Conflict("This upload can no longer be attached.");
                    }

                    poll.AttachMedia(CreateMedia(poll.Id, session));
                }

                poll.MarkPublished();
                await _pollRepository.SaveChangesAsync(transactionToken);
            }, cancellationToken);

            return await ToResponseAsync(poll, creatorId, cancellationToken);
        }

        public Task<PollResponse> UpdateAsync(
            Guid pollId,
            UpdatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return AuditedAsync(
                AuditEvents.Poll.Updated,
                pollId,
                creatorId,
                PollStatus.Draft,
                PollStatus.Draft,
                () => UpdateCoreAsync(pollId, request, creatorId, cancellationToken),
                response => response.Id);
        }

        private async Task<PollResponse> UpdateCoreAsync(
            Guid pollId,
            UpdatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

            var storageToDelete = new List<(string BucketName, string ObjectKey)>();

            await _pollRepository.ExecuteInTransactionAsync(async transactionToken =>
            {
                var poll = await GetOwnedPollAsync(pollId, creatorId, transactionToken);
                if (poll.Status != PollStatus.Draft)
                {
                    throw Conflict("Only a draft poll can be updated.");
                }

                if (request.Question is not null)
                {
                    poll.UpdateQuestion(request.Question.Trim());
                }

                if (request.ExpiresAt is not null)
                {
                    poll.UpdateExpiresAt(ToUtc(request.ExpiresAt));
                }

                if (request.Options is not null)
                {
                    var useClientOrder = request.Options.All(option => option.DisplayOrder is > 0);
                    var options = new List<(string Text, int DisplayOrder)>(request.Options.Count);
                    for (var i = 0; i < request.Options.Count; i++)
                    {
                        var order = useClientOrder ? request.Options[i].DisplayOrder!.Value : i + 1;
                        options.Add((request.Options[i].Text!.Trim(), order));
                    }

                    poll.ReplaceTextOptions(options);
                }

                if (request.MediaUploadSessionIdSpecified)
                {
                    if (request.MediaUploadSessionId is null)
                    {
                        var cleared = poll.ClearMedia();
                        if (cleared is not null)
                        {
                            await _sessions.UnlinkAttachedObjectAsync(
                                cleared.BucketName,
                                cleared.ObjectKey,
                                transactionToken);
                            storageToDelete.Add((cleared.BucketName, cleared.ObjectKey));
                        }
                    }
                    else
                    {
                        await ReplaceMediaAsync(
                            poll,
                            creatorId,
                            request.MediaUploadSessionId.Value,
                            storageToDelete,
                            transactionToken);
                    }
                }

                await _pollRepository.SaveChangesAsync(transactionToken);
            }, cancellationToken);

            foreach (var item in storageToDelete.Distinct())
            {
                if (await _sessions.IsReferencedByPollMediaAsync(item.BucketName, item.ObjectKey, cancellationToken))
                {
                    continue;
                }

                try
                {
                    await _storage.DeleteAsync(item.BucketName, item.ObjectKey, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                }
            }

            var updated = await GetOwnedPollAsync(pollId, creatorId, cancellationToken);
            return await ToResponseAsync(updated, creatorId, cancellationToken);
        }

        public async Task<CreatorPollsPage> GetPollsAsync(
            string? cursor,
            string? contentType,
            CancellationToken cancellationToken = default)
        {
            PollContentType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                if (!Enum.TryParse<PollContentType>(contentType.Trim(), ignoreCase: true, out var value)
                    || !Enum.IsDefined(value))
                {
                    throw Validation("Content type must be question, image, video, or audio.", "contentType");
                }

                parsedType = value;
            }

            var decoded = PollCursor.TryDecode(cursor);
            var take = PollCursor.PageSize + 1;
            var polls = await _pollRepository.GetVisiblePollsAsync(
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                parsedType,
                cancellationToken);

            var hasMore = polls.Count > PollCursor.PageSize;
            var page = polls.Take(PollCursor.PageSize).ToList();
            var counts = await _pollRepository.GetVoteCountsAsync(
                page.Select(poll => poll.Id).ToArray(),
                cancellationToken);

            string? nextCursor = null;
            if (hasMore)
            {
                var last = page[^1];
                nextCursor = PollCursor.Encode(last.CreatedAt, last.Id);
            }

            var items = new List<PollResponse>(page.Count);
            foreach (var poll in page)
            {
                items.Add(await ToResponseAsync(
                    poll,
                    viewerId: null,
                    counts.GetValueOrDefault(poll.Id) ?? PollVoteCountSnapshot.Empty,
                    cancellationToken));
            }

            return new CreatorPollsPage
            {
                Items = items,
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }

        public async Task<PollResponse> GetPollDetailsAsync(
            Guid pollId,
            Guid? viewerId,
            CancellationToken cancellationToken = default)
        {
            var poll = await GetReadablePollAsync(pollId, viewerId, cancellationToken);
            return await ToResponseAsync(poll, viewerId, cancellationToken);
        }

        public async Task<PollResponse> GetPollByShareCodeAsync(
            string shareCode,
            Guid? viewerId,
            CancellationToken cancellationToken = default)
        {
            var poll = string.IsNullOrWhiteSpace(shareCode) || shareCode.Length > ShareCodeGenerator.MaxLength
                ? null
                : await _pollRepository.GetByShareCodeAsync(shareCode, cancellationToken);
            if (poll is null)
            {
                throw NotFound("Poll not found");
            }

            EnsureReadable(poll, viewerId);
            return await ToResponseAsync(poll, viewerId, cancellationToken);
        }

        public async Task<PollResultsResponse> GetResultsAsync(
            Guid pollId,
            Guid? viewerId,
            CancellationToken cancellationToken = default)
        {
            var poll = await GetReadablePollAsync(pollId, viewerId, cancellationToken);
            var counts = await _pollRepository.GetVoteCountsAsync([poll.Id], cancellationToken);
            var snapshot = (counts.GetValueOrDefault(poll.Id) ?? PollVoteCountSnapshot.Empty).ForDisplay();
            return new PollResultsResponse
            {
                PollId = poll.Id,
                TotalVoteCount = snapshot.TotalVotes,
                MinVotes = snapshot.MinVotes,
                VoteProgress = snapshot.OtherVotes,
                ResultsUnlocked = snapshot.ResultsUnlocked,
                CustomAnswerCount = snapshot.CustomAnswerCount,
                TotalWhyCount = snapshot.WhyCount,
                Options = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new PollOptionResultResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        DisplayOrder = option.SortOrder,
                        VoteCount = snapshot.CountFor(option.Id)
                    })
                    .ToList()
            };
        }

        public Task<PollResponse> PublishAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return AuditedAsync(
                AuditEvents.Poll.Published,
                pollId,
                creatorId,
                PollStatus.Draft,
                PollStatus.Active,
                () => PublishCoreAsync(pollId, creatorId, cancellationToken),
                response => response.Id);
        }

        private async Task<PollResponse> PublishCoreAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            await _pollRepository.ExecuteInTransactionAsync(async transactionToken =>
            {
                var poll = await GetOwnedPollAsync(pollId, creatorId, transactionToken);
                if (poll.Status != PollStatus.Draft)
                {
                    throw Conflict("Only a draft poll can be published.");
                }

                if (poll.ExpiresAt is not null && poll.ExpiresAt <= DateTime.UtcNow)
                {
                    throw Validation("This poll has expired.");
                }

                if (poll.Media is null && poll.ContentType != PollContentType.Question)
                {
                    await AttachCurrentSessionAsync(poll, creatorId, transactionToken);
                }

                var blocker = poll.GetPublishBlocker();
                if (blocker is not null)
                {
                    throw poll.ContentType == PollContentType.Question || poll.Media is not null
                        ? Validation(blocker)
                        : Conflict(blocker);
                }

                var published = await _pollRepository.TryMarkActiveAsync(poll.Id, creatorId, transactionToken);
                if (published != 1)
                {
                    throw Conflict("Only a draft poll can be published.");
                }

                poll.MarkPublished();
            }, cancellationToken);

            var publishedPoll = await GetOwnedPollAsync(pollId, creatorId, cancellationToken);
            return await ToResponseAsync(publishedPoll, creatorId, cancellationToken);
        }

        public Task<PollResponse> ClosePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return AuditedAsync(
                AuditEvents.Poll.Closed,
                pollId,
                creatorId,
                null,
                PollStatus.Closed,
                () => CloseCoreAsync(pollId, creatorId, cancellationToken),
                response => response.Id);
        }

        private async Task<PollResponse> CloseCoreAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            var poll = await GetOwnedPollAsync(pollId, creatorId, cancellationToken);

            if (poll.Status == PollStatus.Closed)
            {
                throw Conflict("Poll is already closed.");
            }

            if (!poll.TryClose())
            {
                throw Validation("Poll cannot be closed in its current state.");
            }

            await _pollRepository.SaveChangesAsync(cancellationToken);
            return await ToResponseAsync(poll, creatorId, cancellationToken);
        }

        public async Task DeletePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            await AuditedAsync(
                AuditEvents.Poll.Deleted,
                pollId,
                creatorId,
                null,
                null,
                async () =>
                {
                    await DeleteCoreAsync(pollId, creatorId, cancellationToken);
                    return pollId;
                },
                id => id);
        }

        private async Task DeleteCoreAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            var keys = new List<StoredObjectReference>();
            await _pollRepository.ExecuteInTransactionAsync(async transactionToken =>
            {
                var poll = await GetOwnedPollAsync(pollId, creatorId, transactionToken);
                if (poll.Status is not (PollStatus.Draft or PollStatus.Active))
                {
                    throw Conflict("Only a draft or active poll can be deleted.");
                }

                if (poll.Media is not null)
                {
                    keys.Add(new StoredObjectReference(Guid.Empty, poll.Media.BucketName, poll.Media.ObjectKey));
                }

                keys.AddRange(await _sessions.DetachForDeletedDraftAsync(poll.Id, transactionToken));
                _pollRepository.Delete(poll);
                await _pollRepository.SaveChangesAsync(transactionToken);
            }, cancellationToken);

            foreach (var group in keys.GroupBy(item => (item.BucketName, item.ObjectKey)))
            {
                if (await _sessions.IsReferencedByPollMediaAsync(group.Key.BucketName, group.Key.ObjectKey, cancellationToken))
                {
                    continue;
                }

                try
                {
                    await _storage.DeleteAsync(group.Key.BucketName, group.Key.ObjectKey, cancellationToken);
                    foreach (var item in group.Where(item => item.SessionId != Guid.Empty))
                    {
                        await _sessions.MarkPurgedAsync(item.SessionId, cancellationToken);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    foreach (var item in group.Where(item => item.SessionId != Guid.Empty))
                    {
                        await _sessions.RecordFailureAsync(
                            item.SessionId,
                            MediaFailure.Sanitize("Storage delete failed."),
                            cancellationToken);
                    }
                }
            }
        }

        private async Task ReplaceMediaAsync(
            PollEntity poll,
            Guid creatorId,
            Guid sessionId,
            List<(string BucketName, string ObjectKey)> storageToDelete,
            CancellationToken cancellationToken)
        {
            if (poll.ContentType == PollContentType.Question)
            {
                throw Validation("Question polls cannot have media.");
            }

            var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is null || session.UserId != creatorId)
            {
                throw NotFound("Upload session not found");
            }

            if (!Matches(poll.ContentType, session.MediaType))
            {
                throw Validation("Media type does not match the poll content type.");
            }

            if (session.Status is MediaUploadStatus.Cancelled
                or MediaUploadStatus.Failed
                or MediaUploadStatus.Expired
                or MediaUploadStatus.Attached)
            {
                throw Conflict("This upload can no longer be attached.");
            }

            if (await _sessions.HasBlockingSelectionAsync(
                    MediaOwner.ForUser(creatorId),
                    session.PollId,
                    session.SelectionVersion,
                    session.Id,
                    includeEqual: false,
                    cancellationToken))
            {
                throw Conflict("This upload belongs to an older media selection.");
            }

            var previous = poll.ClearMedia();
            if (previous is not null)
            {
                await _sessions.UnlinkAttachedObjectAsync(
                    previous.BucketName,
                    previous.ObjectKey,
                    cancellationToken);
                storageToDelete.Add((previous.BucketName, previous.ObjectKey));
            }

            if (session.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading)
            {
                var linked = await _sessions.TryLinkPendingAsync(session.Id, creatorId, poll.Id, cancellationToken);
                if (!linked)
                {
                    throw Conflict("This upload can no longer be attached.");
                }

                return;
            }

            var attached = await _sessions.TryMarkAttachedAsync(session.Id, creatorId, poll.Id, cancellationToken);
            if (!attached)
            {
                throw Conflict("This upload can no longer be attached.");
            }

            poll.AttachMedia(CreateMedia(poll.Id, session));
        }

        private async Task AttachCurrentSessionAsync(
            PollEntity poll,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            var current = await _sessions.GetCurrentForPollAsync(poll.Id, creatorId, cancellationToken);
            if (current is null)
            {
                throw Conflict("This poll needs one matching media item before it can be published.");
            }

            if (current.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading)
            {
                throw Conflict("Media is still uploading.");
            }

            if (current.Status != MediaUploadStatus.Uploaded || !poll.MatchesMedia(current.MediaType))
            {
                throw Conflict("This poll needs one matching media item before it can be published.");
            }

            var attached = await _sessions.TryMarkAttachedAsync(current.Id, creatorId, poll.Id, cancellationToken);
            if (!attached)
            {
                throw Conflict("This upload can no longer be attached.");
            }

            poll.AttachMedia(CreateMedia(poll.Id, current));
            await _pollRepository.SaveChangesAsync(cancellationToken);
        }

        private async Task<PollEntity> GetReadablePollAsync(
            Guid pollId,
            Guid? viewerId,
            CancellationToken cancellationToken)
        {
            var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken);
            if (poll is null)
            {
                throw NotFound("Poll not found");
            }

            EnsureReadable(poll, viewerId);
            return poll;
        }

        private static void EnsureReadable(PollEntity poll, Guid? viewerId)
        {
            var isOwner = viewerId is Guid userId && userId == poll.CreatorId;
            var isActive = poll.Status == PollStatus.Active;
            if (!isActive && !isOwner)
            {
                throw NotFound("Poll not found");
            }
        }

        private async Task<PollEntity> GetOwnedPollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            var poll = await _pollRepository.GetByIdAndCreatorAsync(pollId, creatorId, cancellationToken);
            if (poll is null)
            {
                // The caller still gets a plain 404 (no existence leak), but an attempt on someone
                // else's poll is recorded as a permission failure.
                if (await _pollRepository.GetByIdAsync(pollId, cancellationToken) is not null)
                {
                    await _audit.WritePollAsync(
                        new PollAuditEntry(
                            pollId,
                            AuditActorType.User,
                            creatorId,
                            AuditEvents.Poll.PermissionDenied,
                            AuditOutcome.Denied,
                            ErrorCode: ApiErrorCodes.NotFound),
                        CancellationToken.None);
                }

                throw NotFound("Poll not found");
            }

            return poll;
        }

        private async Task<T> AuditedAsync<T>(
            string eventType,
            Guid? pollId,
            Guid actorId,
            PollStatus? fromStatus,
            PollStatus? toStatus,
            Func<Task<T>> action,
            Func<T, Guid?> pollIdOf)
        {
            T result;
            try
            {
                result = await action();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var errorCode = exception switch
                {
                    AppException app => app.ErrorCode,
                    ValidationException => ApiErrorCodes.ValidationError,
                    _ => exception.GetType().Name
                };

                await _audit.WritePollAsync(
                    new PollAuditEntry(
                        pollId,
                        AuditActorType.User,
                        actorId,
                        AuditEvents.Poll.OperationFailed,
                        AuditOutcome.Failure,
                        fromStatus?.ToString().ToLowerInvariant(),
                        toStatus?.ToString().ToLowerInvariant(),
                        errorCode,
                        Metadata: new Dictionary<string, string?> { ["attempted"] = eventType }),
                    CancellationToken.None);
                throw;
            }

            await _audit.WritePollAsync(
                new PollAuditEntry(
                    pollId ?? pollIdOf(result),
                    AuditActorType.User,
                    actorId,
                    eventType,
                    AuditOutcome.Success,
                    fromStatus?.ToString().ToLowerInvariant(),
                    toStatus?.ToString().ToLowerInvariant()),
                CancellationToken.None);
            return result;
        }

        private async Task<PollResponse> ToResponseAsync(
            PollEntity poll,
            Guid? viewerId,
            CancellationToken cancellationToken)
        {
            var counts = await _pollRepository.GetVoteCountsAsync([poll.Id], cancellationToken);
            return await ToResponseAsync(
                poll,
                viewerId,
                counts.GetValueOrDefault(poll.Id) ?? PollVoteCountSnapshot.Empty,
                cancellationToken);
        }

        private async Task<PollResponse> ToResponseAsync(
            PollEntity poll,
            Guid? viewerId,
            PollVoteCountSnapshot counts,
            CancellationToken cancellationToken)
        {
            var response = poll.ToResponse(counts);
            if (poll.Media is not null && response.Media is not null)
            {
                var download = await _storage.CreateDownloadUrlAsync(
                    poll.Media.BucketName,
                    poll.Media.ObjectKey,
                    _policy.DownloadUrlLifetime,
                    cancellationToken);
                response.Media.Url = download.Url;
                response.Media.UrlExpiresAt = download.ExpiresAt;
                response.MediaReadiness = "ready";
                return response;
            }

            if (viewerId == poll.CreatorId)
            {
                var current = await _sessions.GetCurrentForPollAsync(poll.Id, poll.CreatorId, cancellationToken);
                if (current?.Status is MediaUploadStatus.Pending or MediaUploadStatus.Uploading)
                {
                    response.MediaReadiness = "pending";
                }
            }

            return response;
        }

        private static PollMedia CreateMedia(Guid pollId, MediaUploadSession session)
        {
            var size = session.ActualFileSize ?? session.ExpectedFileSize ?? 0;
            return new PollMedia(
                pollId,
                session.MediaType,
                session.BucketName,
                session.ObjectKey,
                session.MimeType,
                size,
                durationMs: session.DurationMs,
                thumbnailObjectKey: null);
        }

        private static bool Matches(PollContentType contentType, PollMediaType mediaType)
        {
            return contentType switch
            {
                PollContentType.Image => mediaType == PollMediaType.Image,
                PollContentType.Video => mediaType == PollMediaType.Video,
                PollContentType.Audio => mediaType == PollMediaType.Audio,
                _ => false
            };
        }

        private static DateTime? ToUtc(DateTime? value)
        {
            if (value is null)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
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

        private static AppException Validation(string message, string? field = null)
        {
            return new AppException(400, ApiErrorCodes.ValidationError, message, field);
        }
    }
}
