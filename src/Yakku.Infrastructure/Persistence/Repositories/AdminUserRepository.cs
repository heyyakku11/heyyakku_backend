using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminUsers.DTOs;
using Yakku.Application.AdminUsers.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AdminUserRepository : IAdminUserRepository
    {
        private readonly YakkuDbContext _context;

        public AdminUserRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<AdminPage<AdminUserResponse>> GetUsersAsync(
            UserStatus? status, (DateTime CreatedAt, Guid Id)? after, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Users.AsNoTracking();
            if (status.HasValue)
            {
                query = query.Where(user => user.Status == status.Value);
            }

            if (after is { } position)
            {
                query = query.Where(user => user.CreatedAt < position.CreatedAt
                    || (user.CreatedAt == position.CreatedAt && user.Id.CompareTo(position.Id) > 0));
            }

            // One extra row tells whether another page follows.
            var rows = await query
                .OrderByDescending(user => user.CreatedAt)
                .ThenBy(user => user.Id)
                .Take(pageSize + 1)
                .Select(user => new
                {
                    user.Id,
                    user.Profile.AnonymousName,
                    user.PhoneNumber,
                    PlanCode = user.Plan.Code,
                    PlanName = user.Plan.Name,
                    user.Status,
                    user.LoginAt,
                    user.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var hasMore = rows.Count > pageSize;
            return new AdminPage<AdminUserResponse>
            {
                HasMore = hasMore,
                Items = rows.Take(pageSize).Select(row => new AdminUserResponse
                {
                    Id = row.Id,
                    AnonymousName = row.AnonymousName,
                    PhoneNumber = row.PhoneNumber,
                    Plan = new AdminUserPlanResponse { Code = row.PlanCode, Name = row.PlanName },
                    Status = row.Status.ToString().ToLowerInvariant(),
                    AvatarUrl = null,
                    LastLoginAt = row.LoginAt,
                    CreatedAt = row.CreatedAt
                }).ToList()
            };
        }

        public async Task<AdminPage<AdminGuestResponse>> GetGuestsAsync(
            bool? activeOnly, DateTime utcNow, (DateTime CreatedAt, Guid Id)? after, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Guests.AsNoTracking();
            if (activeOnly == true)
            {
                query = query.Where(guest => guest.ExpiresAt > utcNow);
            }
            else if (activeOnly == false)
            {
                query = query.Where(guest => guest.ExpiresAt <= utcNow);
            }

            if (after is { } position)
            {
                query = query.Where(guest => guest.CreatedAt < position.CreatedAt
                    || (guest.CreatedAt == position.CreatedAt && guest.Id.CompareTo(position.Id) > 0));
            }

            var rows = await query
                .OrderByDescending(guest => guest.CreatedAt)
                .ThenBy(guest => guest.Id)
                .Take(pageSize + 1)
                .Select(guest => new
                {
                    guest.Id,
                    guest.CreatedAt,
                    guest.ExpiresAt,
                    guest.LastSeenAt,
                    PollVotes = _context.PollVotes.Count(vote =>
                        vote.VoterRole == VoterRole.Guest && vote.VoterId == guest.Id),
                    CheckVotes = _context.UserCheckVotes.Count(vote =>
                        vote.VoterRole == VoterRole.Guest && vote.VoterId == guest.Id)
                })
                .ToListAsync(cancellationToken);

            var hasMore = rows.Count > pageSize;
            rows = rows.Take(pageSize).ToList();

            // The most recently seen installation of each guest on this page, in one extra query.
            var ids = rows.Select(row => row.Id).ToList();
            var devices = await _context.Devices
                .AsNoTracking()
                .Where(device => device.GuestId != null && ids.Contains(device.GuestId.Value))
                .OrderByDescending(device => device.LastSeenAt)
                .Select(device => new
                {
                    GuestId = device.GuestId!.Value,
                    device.Platform,
                    device.DeviceModel,
                    device.OsVersion,
                    device.AppVersion
                })
                .ToListAsync(cancellationToken);
            var latestDevice = devices
                .GroupBy(device => device.GuestId)
                .ToDictionary(group => group.Key, group => group.First());

            return new AdminPage<AdminGuestResponse>
            {
                HasMore = hasMore,
                Items = rows.Select(row => new AdminGuestResponse
                {
                    Id = row.Id,
                    CreatedAt = row.CreatedAt,
                    Status = row.ExpiresAt > utcNow ? "active" : "expired",
                    ExpiresAt = row.ExpiresAt,
                    LastSeenAt = row.LastSeenAt,
                    CardVotes = row.PollVotes + row.CheckVotes,
                    Device = latestDevice.TryGetValue(row.Id, out var device)
                        ? new AdminGuestDeviceResponse
                        {
                            Platform = device.Platform.ToString(),
                            DeviceModel = device.DeviceModel,
                            OsVersion = device.OsVersion,
                            AppVersion = device.AppVersion
                        }
                        : null
                }).ToList()
            };
        }
    }
}
