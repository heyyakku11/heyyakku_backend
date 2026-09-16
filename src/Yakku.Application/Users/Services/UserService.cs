using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Mapper;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Mapper;
using Yakku.Application.Votes.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Users.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPollRepository _pollRepository;
        private readonly IVoteRepository _voteRepository;

        public UserService(
            IUserRepository userRepository,
            IPollRepository pollRepository,
            IVoteRepository voteRepository)
        {
            _userRepository = userRepository;
            _pollRepository = pollRepository;
            _voteRepository = voteRepository;
        }

        public async Task<UserProfileResponse?> GetMeAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            return user?.ToProfileResponse();
        }

        public async Task<UserPollsPage> GetAskedPollsAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var decoded = PollCursor.TryDecode(cursor);
            var take = PollCursor.PageSize + 1;
            var polls = await _pollRepository.GetCreatedByUserAsync(
                userId,
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                cancellationToken);

            var hasMore = polls.Count > PollCursor.PageSize;
            var items = polls
                .Take(PollCursor.PageSize)
                .Select(poll => poll.ToUserPollResponse())
                .ToList();

            string? nextCursor = null;
            if (hasMore)
            {
                var last = polls[PollCursor.PageSize - 1];
                nextCursor = PollCursor.Encode(last.CreatedAt, last.Id);
            }

            return new UserPollsPage
            {
                Items = items,
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }

        public async Task<UserPollsPage> GetAnsweredPollsAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var decoded = PollCursor.TryDecode(cursor);
            var take = PollCursor.PageSize + 1;
            var entries = await _pollRepository.GetAnsweredByUserAsync(
                userId,
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                cancellationToken);

            var hasMore = entries.Count > PollCursor.PageSize;
            var page = entries.Take(PollCursor.PageSize).ToList();
            var items = page
                .Select(entry => entry.Poll.ToUserPollResponse())
                .ToList();

            string? nextCursor = null;
            if (hasMore)
            {
                var last = page[^1];
                nextCursor = PollCursor.Encode(last.VotedAt, last.Poll.Id);
            }

            return new UserPollsPage
            {
                Items = items,
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }

        public async Task<UserPollDetailResponse> GetOwnedPollDetailsAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            var poll = await _pollRepository.GetByIdAndCreatorAsync(pollId, userId, cancellationToken);
            if (poll is null || poll.Status == PollStatus.Deleted)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Poll not found");
            }

            var creatorVote = await _voteRepository.GetByUserAndPollAsync(
                userId,
                pollId,
                cancellationToken);

            return poll.ToUserPollDetailResponse(creatorVote);
        }
    }
}
