using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Mapper;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Mapper;

namespace Yakku.Application.Users.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPollRepository _pollRepository;

        public UserService(
            IUserRepository userRepository,
            IPollRepository pollRepository)
        {
            _userRepository = userRepository;
            _pollRepository = pollRepository;
        }

        public async Task<UserProfileResponse?> GetMeAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            return user?.ToProfileResponse();
        }

        public async Task<UserPollsPage> GetMyPollsAsync(
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
    }
}
