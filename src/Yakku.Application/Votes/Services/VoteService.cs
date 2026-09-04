using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Interfaces;
using Yakku.Application.Votes.Mapper;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Votes.Services
{
    public class VoteService : IVoteService
    {
        private readonly IPollRepository _pollRepository;
        private readonly IVoteRepository _voteRepository;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<CastVoteRequest> _validator;

        public VoteService(
            IPollRepository pollRepository,
            IVoteRepository voteRepository,
            ISystemLogWriter systemLogWriter,
            IValidator<CastVoteRequest> validator)
        {
            _pollRepository = pollRepository;
            _voteRepository = voteRepository;
            _systemLogWriter = systemLogWriter;
            _validator = validator;
        }

        public async Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid guestId,
            CastVoteRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken);
            if (poll is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Poll not found");
            }

            if (!IsAcceptingVotes(poll))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This poll is not accepting votes.");
            }

            Guid? pollOptionId = null;
            string? customOptionText = null;

            if (request.OptionId is not null)
            {
                var option = poll.Options.FirstOrDefault(item => item.Id == request.OptionId.Value);
                if (option is null)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Option does not belong to this poll.",
                        "optionId");
                }

                pollOptionId = option.Id;
            }
            else
            {
                customOptionText = request.CustomOption!.Trim();
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? null
                : request.Reason.Trim();

            if (await _voteRepository.ExistsAsync(guestId, pollId, cancellationToken))
            {
                await LogAlreadyVotedAsync(guestId, pollId, cancellationToken);
                throw VoteExceptions.AlreadyVoted();
            }

            var vote = new Vote(guestId, pollId, pollOptionId, customOptionText, reason);
            await _voteRepository.AddAsync(vote, cancellationToken);
            try
            {
                await _voteRepository.SaveChangesAsync(cancellationToken);
            }
            catch (AppException exception) when (exception.ErrorCode == ApiErrorCodes.AlreadyVoted)
            {
                await LogAlreadyVotedAsync(guestId, pollId, cancellationToken);
                throw;
            }

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.VoteCast,
                    Message = "Vote submitted.",
                    GuestId = guestId,
                    Details = new
                    {
                        pollId,
                        voteId = vote.Id,
                        pollOptionId,
                        hasCustomOption = customOptionText is not null
                    }
                },
                cancellationToken);

            return vote.ToResponse();
        }

        private Task LogAlreadyVotedAsync(Guid guestId, Guid pollId, CancellationToken cancellationToken)
        {
            return _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Warning,
                    EventType = SystemLogEventTypes.VoteRejectedAlreadyVoted,
                    Message = "Duplicate vote rejected.",
                    GuestId = guestId,
                    Details = new { pollId }
                },
                cancellationToken);
        }

        private static bool IsAcceptingVotes(PollEntity poll)
        {
            if (poll.Status != PollStatus.Active)
            {
                return false;
            }

            return poll.ExpiresAt is null || poll.ExpiresAt > DateTime.UtcNow;
        }
    }
}
