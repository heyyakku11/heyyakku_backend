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
            UserStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Users.AsNoTracking();
            if (status.HasValue)
            {
                query = query.Where(user => user.Status == status.Value);
            }

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(user => user.CreatedAt)
                .ThenBy(user => user.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(user => new
                {
                    user.Id,
                    user.AnonymousName,
                    user.PhoneNumber,
                    PlanCode = user.UserPlan.Plan.Code,
                    PlanName = user.UserPlan.Plan.Name,
                    user.UserPlan.PlanActivatedAt,
                    user.UserPlan.PlanExpiresAt,
                    user.Status,
                    user.LoginAt,
                    user.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new AdminPage<AdminUserResponse>
            {
                TotalCount = total,
                Items = rows.Select(row => new AdminUserResponse
                {
                    Id = row.Id,
                    AnonymousName = row.AnonymousName,
                    PhoneNumber = row.PhoneNumber,
                    Plan = new AdminUserPlanResponse
                    {
                        Code = row.PlanCode,
                        Name = row.PlanName,
                        ActivatedAt = row.PlanActivatedAt,
                        ExpiresAt = row.PlanExpiresAt
                    },
                    Status = row.Status.ToString().ToLowerInvariant(),
                    AvatarUrl = null,
                    LastLoginAt = row.LoginAt,
                    CreatedAt = row.CreatedAt
                }).ToList()
            };
        }

        public async Task<AdminPage<AdminGuestResponse>> GetGuestsAsync(
            bool? activeOnly, DateTime utcNow, int page, int pageSize, CancellationToken cancellationToken = default)
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

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(guest => guest.CreatedAt)
                .ThenBy(guest => guest.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            return new AdminPage<AdminGuestResponse>
            {
                TotalCount = total,
                Items = rows.Select(row => new AdminGuestResponse
                {
                    Id = row.Id,
                    CreatedAt = row.CreatedAt,
                    Status = row.ExpiresAt > utcNow ? "active" : "expired",
                    ExpiresAt = row.ExpiresAt,
                    LastSeenAt = row.LastSeenAt,
                    CardVotes = row.PollVotes + row.CheckVotes,
                    Device = null
                }).ToList()
            };
        }
    }
}
