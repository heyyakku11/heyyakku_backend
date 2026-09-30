using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.YakkuDirectory.DTOs;
using Yakku.Application.YakkuDirectory.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.YakkuDirectory.Services
{
    public class YakkuDirectoryService : IYakkuDirectoryService
    {
        private readonly IUserRepository _users;
        private readonly IGuestRepository _guests;
        private readonly IPollRepository _polls;

        public YakkuDirectoryService(
            IUserRepository users,
            IGuestRepository guests,
            IPollRepository polls)
        {
            _users = users;
            _guests = guests;
            _polls = polls;
        }

        public async Task<IReadOnlyList<YakkuUserProfileResponse>> ListUsersAsync(
            string? status,
            CancellationToken cancellationToken = default)
        {
            var filter = YakkuStatusFilter.ParseUserStatus(status);
            var users = await _users.ListAsync(filter, cancellationToken);

            return users
                .Select(user => new YakkuUserProfileResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.Profile?.DisplayName ?? string.Empty,
                    Status = user.Status == UserStatus.Active ? "active" : "nonactive",
                    LastLoginAt = user.LastLoginAt,
                    CreatedAt = user.CreatedAt
                })
                .ToList();
        }

        public async Task<IReadOnlyList<YakkuGuestProfileResponse>> ListGuestsAsync(
            string? status,
            CancellationToken cancellationToken = default)
        {
            var active = YakkuStatusFilter.ParseGuestActivity(status);
            var utcNow = DateTime.UtcNow;
            var guests = await _guests.ListAsync(active, utcNow, cancellationToken);

            return guests
                .Select(guest => new YakkuGuestProfileResponse
                {
                    Id = guest.Id,
                    Status = guest.ExpiresAt > utcNow ? "active" : "nonactive",
                    CreatedAt = guest.CreatedAt,
                    LastSeenAt = guest.LastSeenAt,
                    ExpiresAt = guest.ExpiresAt
                })
                .ToList();
        }

        public async Task<IReadOnlyList<YakkuPollResponse>> ListPollsAsync(
            string? status,
            CancellationToken cancellationToken = default)
        {
            var filter = YakkuStatusFilter.ParsePollStatus(status);
            var polls = await _polls.ListByStatusAsync(filter, cancellationToken);

            return polls
                .Select(poll => new YakkuPollResponse
                {
                    Id = poll.Id,
                    CreatorId = poll.CreatorId,
                    Question = poll.Question,
                    Status = poll.Status.ToString().ToLowerInvariant(),
                    OptionType = poll.OptionType.ToString().ToLowerInvariant(),
                    TotalVoteCount = poll.TotalVoteCount,
                    ExpiresAt = poll.ExpiresAt,
                    CreatedAt = poll.CreatedAt
                })
                .ToList();
        }
    }
}
