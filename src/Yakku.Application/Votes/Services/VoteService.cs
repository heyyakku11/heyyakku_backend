using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.Interfaces;
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
        private readonly IImageRepository _imageRepository;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<CastVoteRequest> _validator;

        public VoteService(
            IPollRepository pollRepository,
            IVoteRepository voteRepository,
            IImageRepository imageRepository,
            ISystemLogWriter systemLogWriter,
            IValidator<CastVoteRequest> validator)
        {
            _pollRepository = pollRepository;
            _voteRepository = voteRepository;
            _imageRepository = imageRepository;
            _systemLogWriter = systemLogWriter;
            _validator = validator;
        }

        public async Task<VoteResponse> CastAsync(
            Guid pollId,
            Guid? userId,
            Guid? guestId,
            CastVoteRequest request,
            CancellationToken cancellationToken = default)
        {
            var hasUser = userId is not null && userId != Guid.Empty;
            var hasGuest = guestId is not null && guestId != Guid.Empty;
            if (hasUser == hasGuest)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Exactly one of userId or guestId is required.");
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

            if (!IsAcceptingVotes(poll))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This poll is not accepting votes.");
            }

            Guid? pollOptionId = null;
            string? customOptionText = null;
            Guid? imageId = null;

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
            else if (poll.OptionType == OptionType.Text)
            {
                if (request.ImageId is not null)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Text polls do not accept custom image answers.",
                        "imageId");
                }

                customOptionText = request.CustomOption!.Trim();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.CustomOption))
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Image polls do not accept custom text answers.",
                        "customOption");
                }

                if (request.ImageId is null || request.ImageId == Guid.Empty)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Custom image answer requires imageId.",
                        "imageId");
                }

                var existing = await _imageRepository.GetExistingIdsAsync(
                    [request.ImageId.Value],
                    cancellationToken);
                if (!existing.Contains(request.ImageId.Value))
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Image ID is invalid.",
                        "imageId");
                }

                imageId = request.ImageId.Value;
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? null
                : request.Reason.Trim();

            if (hasUser)
            {
                if (await _voteRepository.ExistsForUserAsync(userId!.Value, pollId, cancellationToken))
                {
                    await LogAlreadyVotedAsync(userId, null, pollId, cancellationToken);
                    throw VoteExceptions.AlreadyVoted();
                }
            }
            else if (await _voteRepository.ExistsForGuestAsync(guestId!.Value, pollId, cancellationToken))
            {
                await LogAlreadyVotedAsync(null, guestId, pollId, cancellationToken);
                throw VoteExceptions.AlreadyVoted();
            }

            var vote = hasUser
                ? Vote.ForUser(userId!.Value, pollId, pollOptionId, customOptionText, reason, imageId)
                : new Vote(guestId!.Value, pollId, pollOptionId, customOptionText, reason, imageId);

            await _voteRepository.AddAsync(vote, cancellationToken);
            try
            {
                await _voteRepository.SaveChangesAsync(cancellationToken);
            }
            catch (AppException exception) when (exception.ErrorCode == ApiErrorCodes.AlreadyVoted)
            {
                await LogAlreadyVotedAsync(userId, guestId, pollId, cancellationToken);
                throw;
            }

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.VoteCast,
                    Message = "Vote submitted.",
                    UserId = userId,
                    GuestId = guestId,
                    Details = new
                    {
                        pollId,
                        voteId = vote.Id,
                        pollOptionId,
                        hasCustomOption = customOptionText is not null,
                        hasCustomImage = imageId is not null
                    }
                },
                cancellationToken);

            return vote.ToResponse();
        }

        private Task LogAlreadyVotedAsync(
            Guid? userId,
            Guid? guestId,
            Guid pollId,
            CancellationToken cancellationToken)
        {
            return _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Warning,
                    EventType = SystemLogEventTypes.VoteRejectedAlreadyVoted,
                    Message = "Duplicate vote rejected.",
                    UserId = userId,
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
