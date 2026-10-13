using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls;
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
                    PhoneNumber = user.PhoneNumber,
                    Name = user.Profile?.Name ?? string.Empty,
                    AnonymousName = user.Profile?.AnonymousName ?? string.Empty,
                    Status = user.Status == UserStatus.Active ? "active" : "nonactive",
                    LoginAt = user.LoginAt,
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
            var counts = await _polls.GetVoteCountsAsync(
                polls.Select(poll => poll.Id).ToArray(),
                cancellationToken);

            return polls
                .Select(poll =>
                {
                    var tally = counts.GetValueOrDefault(poll.Id) ?? PollVoteCountSnapshot.Empty;
                    return new YakkuPollResponse
                    {
                        Id = poll.Id,
                        CreatorId = poll.CreatorId,
                        Question = poll.Question,
                        Status = poll.Status.ToString().ToLowerInvariant(),
                        ContentType = poll.ContentType.ToString().ToLowerInvariant(),
                        Visibility = poll.Visibility.ToString().ToLowerInvariant(),
                        TotalVoteCount = tally.TotalVotes,
                        CustomAnswerCount = tally.CustomAnswerCount,
                        TotalWhyCount = tally.WhyCount,
                        CreatedAt = poll.CreatedAt
                    };
                })
                .ToList();
        }
    }
}
