using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Interfaces;
using Yakku.Application.Votes.Mapper;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Votes.Services
{
    public class VoteService : IVoteService
    {
        private readonly IPollRepository _pollRepository;
        private readonly IVoteRepository _voteRepository;
        private readonly IValidator<CastVoteRequest> _validator;
        private readonly IAuditLogWriter _audit;

        public VoteService(
            IPollRepository pollRepository,
            IVoteRepository voteRepository,
            IValidator<CastVoteRequest> validator,
            IAuditLogWriter audit)
        {
            _pollRepository = pollRepository;
            _voteRepository = voteRepository;
            _validator = validator;
            _audit = audit;
        }

        public async Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid userId,
            CastVoteRequest request,
            CancellationToken cancellationToken = default)
        {
            // Only the voter reference, poll and outcome are audited. The chosen option, custom answer and
            // reason are deliberately never written.
            try
            {
                var response = await CastCoreAsync(pollId, userId, request, cancellationToken);
                await _audit.WritePollAsync(
                    new PollAuditEntry(
                        pollId,
                        AuditActorType.User,
                        null,
                        AuditEvents.Poll.VoteCast,
                        AuditOutcome.Success,
                        VoterUserId: userId),
                    CancellationToken.None);
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

                await _audit.WritePollAsync(
                    new PollAuditEntry(
                        pollId,
                        AuditActorType.User,
                        null,
                        AuditEvents.Poll.VoteRejected,
                        errorCode == ApiErrorCodes.Unauthorized ? AuditOutcome.Denied : AuditOutcome.Failure,
                        ErrorCode: errorCode,
                        VoterUserId: userId == Guid.Empty ? null : userId),
                    CancellationToken.None);
                throw;
            }
        }

        private async Task<VoteResponse> CastCoreAsync(
            Guid pollId,
            Guid userId,
            CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
            {
                throw new AppException(
                    401,
                    ApiErrorCodes.Unauthorized,
                    "An authenticated voter is required.");
            }

            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken);
            if (poll is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Poll not found");
            }

            if (poll.Status != PollStatus.Active)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This poll is not accepting votes.");
            }

            if (poll.ExpiresAt is not null && poll.ExpiresAt <= DateTime.UtcNow)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This poll has expired.");
            }

            PollVote vote;
            if (request.PollOptionId is not null)
            {
                var option = poll.Options.FirstOrDefault(item => item.Id == request.PollOptionId.Value);
                if (option is null)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Option does not belong to this poll.",
                        "pollOptionId");
                }

                vote = PollVote.ForOption(
                    userId,
                    pollId,
                    option.Id,
                    NormalizeOptional(request.Reason));
            }
            else
            {
                vote = PollVote.ForCustomAnswer(
                    userId,
                    pollId,
                    request.CustomAnswer!.Trim(),
                    NormalizeOptional(request.Reason));
            }

            if (await _voteRepository.ExistsForUserAsync(userId, pollId, cancellationToken))
            {
                throw VoteExceptions.AlreadyVoted();
            }

            await _voteRepository.AddAsync(vote, cancellationToken);
            await _voteRepository.SaveChangesAsync(cancellationToken);
            return vote.ToResponse();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
