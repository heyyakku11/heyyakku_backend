using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.Storage;
using Yakku.Application.Streaks;
using Yakku.Application.Streaks.Interfaces;
using Yakku.Application.Votes;
using Yakku.Domain.Enums;

namespace Yakku.Application.Sharing.Services
{
    public class VotingService : IVotingService
    {
        private readonly IVotableContentRepository _contents;
        private readonly IObjectStorage _storage;
        private readonly IMediaUploadPolicy _policy;
        private readonly IValidator<VoteRequest> _validator;
        private readonly IAuditLogWriter _audit;
        private readonly IStreakService _streaks;

        public VotingService(
            IVotableContentRepository contents,
            IObjectStorage storage,
            IMediaUploadPolicy policy,
            IValidator<VoteRequest> validator,
            IAuditLogWriter audit,
            IStreakService streaks)
        {
            _streaks = streaks;
            _contents = contents;
            _storage = storage;
            _policy = policy;
            _validator = validator;
            _audit = audit;
        }

        public async Task<SharedContentResponse> VoteAsync(
            VoteRequest request,
            VoterIdentity voter,
            CancellationToken cancellationToken = default)
        {
            return (await CastAsync(request, voter, expectedType: null, cancellationToken)).Content;
        }

        public async Task<VoteOutcome> CastAsync(
            VoteRequest request,
            VoterIdentity voter,
            ShareContentType? expectedType = null,
            CancellationToken cancellationToken = default)
        {
            // Only the voter reference, the content and the outcome are audited. The chosen option, custom answer
            // and reason are deliberately never written.
            Guid? contentId = null;
            string? contentType = null;
            try
            {
                var content = await FindAsync(request, voter, cancellationToken);
                if (expectedType is ShareContentType expected && content.ContentType != expected)
                {
                    throw SharedContentService.NotFound();
                }

                contentId = content.ContentId;
                contentType = content.IsPoll ? ContentTypes.Poll : ContentTypes.Check;

                var response = await VoteCoreAsync(request, voter, content, cancellationToken);
                await AuditAsync(AuditEvents.Poll.VoteCast, AuditOutcome.Success, contentId, contentType, voter, null);
                return response;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var errorCode = exception switch
                {
                    AppException app => app.ErrorCode,
                    ValidationException => ApiErrorCodes.ValidationError,
                    _ => exception.GetType().Name
                };

                var outcome = errorCode is ApiErrorCodes.Unauthorized or ApiErrorCodes.Forbidden
                    ? AuditOutcome.Denied
                    : AuditOutcome.Failure;
                await AuditAsync(AuditEvents.Poll.VoteRejected, outcome, contentId, contentType, voter, errorCode);
                throw;
            }
        }

        private async Task<VotableContent> FindAsync(
            VoteRequest request,
            VoterIdentity voter,
            CancellationToken cancellationToken)
        {
            if (voter.Id == Guid.Empty)
            {
                throw new AppException(
                    401,
                    ApiErrorCodes.Unauthorized,
                    "A signed-in user or a guest session is required.");
            }

            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var content = await _contents.FindByShareCodeAsync(request.ShareCode!.Trim(), cancellationToken);
            if (content is null)
            {
                throw SharedContentService.NotFound();
            }

            return content;
        }

        private async Task<VoteOutcome> VoteCoreAsync(
            VoteRequest request,
            VoterIdentity voter,
            VotableContent content,
            CancellationToken cancellationToken)
        {
            var isOwner = voter.IsUser && voter.Id == content.OwnerId;
            if (!content.PubliclyReadable && !isOwner)
            {
                throw SharedContentService.NotFound();
            }

            if (isOwner && !content.AllowOwnerVote)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.Forbidden,
                    "You cannot answer your own check.");
            }

            if (content.ClosedReason(DateTime.UtcNow) is { } closed)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    closed switch
                    {
                        "EXPIRED" => $"This {Noun(content)} has expired.",
                        _ => $"This {Noun(content)} is not accepting votes."
                    });
            }

            if (request.OptionId is Guid optionId && content.Options.All(option => option.Id != optionId))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    $"Option does not belong to this {Noun(content)}.",
                    "optionId");
            }

            // Cheap early answer; the unique index inside the repository transaction is the authority.
            if (await _contents.GetVoteAsync(content, voter, cancellationToken) is not null)
            {
                throw VoteExceptions.AlreadyVoted();
            }

            var vote = new NewVote(
                request.OptionId,
                request.OptionId is null ? request.CustomAnswer!.Trim() : null,
                string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());

            // A signed-in user's accepted vote earns streak credit in the same transaction as the vote itself, so a
            // rejected, duplicate or rolled-back vote cannot. Guests never earn streaks (the id is not a user).
            Func<VoteReceipt, CancellationToken, Task>? creditStreak = voter.IsUser
                ? (receipt, token) => _streaks.RecordAsync(voter.Id, StreakActivity.VoteCast, receipt.CreatedAt, token)
                : null;

            var receipt = await _contents.AddVoteAsync(content, voter, vote, creditStreak, cancellationToken);

            var response = await SharedContentService.BuildAsync(
                content,
                voter,
                _contents,
                _storage,
                _policy,
                cancellationToken);
            return new VoteOutcome(response, receipt);
        }

        private static string Noun(VotableContent content) => content.IsPoll ? "poll" : "check";

        private Task AuditAsync(
            string eventType,
            AuditOutcome outcome,
            Guid? contentId,
            string? contentType,
            VoterIdentity voter,
            string? errorCode)
        {
            return _audit.WritePollAsync(
                new PollAuditEntry(
                    contentId,
                    voter.IsUser ? AuditActorType.User : AuditActorType.Guest,
                    null,
                    eventType,
                    outcome,
                    ErrorCode: errorCode,
                    VoterUserId: voter.Id == Guid.Empty ? null : voter.Id,
                    Metadata: contentType is null
                        ? null
                        : new Dictionary<string, string?> { ["contentType"] = contentType }),
                CancellationToken.None);
        }
    }
}
