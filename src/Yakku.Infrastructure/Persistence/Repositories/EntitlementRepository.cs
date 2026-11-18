using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Entitlements;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class EntitlementRepository : IEntitlementRepository
    {
        private const string OwnerCardConstraint = "IX_card_unlocks_owner_card";

        private readonly YakkuDbContext _context;

        public EntitlementRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<UserPlanState?> GetUserPlanAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var row = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.PlanActivatedAt,
                    user.PlanExpiresAt,
                    Plan = new PlanPerks(
                        user.Plan.Id,
                        user.Plan.Code,
                        user.Plan.Name,
                        user.Plan.DurationDays,
                        user.Plan.UnlockTickets,
                        user.Plan.AskBackMessageLimit)
                })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? null : new UserPlanState(row.Plan, row.PlanActivatedAt, row.PlanExpiresAt);
        }

        public Task<PlanPerks?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default)
        {
            return PerksQuery()
                .Where(plan => plan.Id == planId)
                .Select(ToPerks)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<PlanPerks?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return PerksQuery()
                .Where(plan => plan.Code == code)
                .Select(ToPerks)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<CardUnlock?> FindUnlockAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            return _context.CardUnlocks.FirstOrDefaultAsync(
                unlock => unlock.OwnerUserId == ownerId
                          && unlock.ContentType == contentType
                          && unlock.ContentId == contentId,
                cancellationToken);
        }

        public Task<int> CountPlanUnlocksSinceAsync(
            Guid ownerId,
            DateTime since,
            CancellationToken cancellationToken = default)
        {
            return _context.CardUnlocks
                .AsNoTracking()
                .CountAsync(
                    unlock => unlock.OwnerUserId == ownerId
                              && unlock.Source == CardUnlockSource.Plan
                              && unlock.CreatedAt >= since,
                    cancellationToken);
        }

        public async Task<CardUnlock?> TryAddPlanUnlockAsync(
            CardUnlock unlock,
            DateTime ticketsSince,
            int? tickets,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // One lock per owner until the transaction ends, so counting the tickets and spending one is a single step.
            var lockKey = $"card-unlock:{unlock.OwnerUserId}";
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);

            var existing = await _context.CardUnlocks.FirstOrDefaultAsync(
                item => item.OwnerUserId == unlock.OwnerUserId
                        && item.ContentType == unlock.ContentType
                        && item.ContentId == unlock.ContentId,
                cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            if (tickets is { } max)
            {
                var used = await _context.CardUnlocks.CountAsync(
                    item => item.OwnerUserId == unlock.OwnerUserId
                            && item.Source == CardUnlockSource.Plan
                            && item.CreatedAt >= ticketsSince,
                    cancellationToken);
                if (used >= max)
                {
                    return null;
                }
            }

            _context.CardUnlocks.Add(unlock);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return unlock;
        }

        public async Task<CardUnlock> AddUnlockAsync(CardUnlock unlock, CancellationToken cancellationToken = default)
        {
            _context.CardUnlocks.Add(unlock);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return unlock;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, OwnerCardConstraint))
            {
                _context.Entry(unlock).State = EntityState.Detached;
                return await _context.CardUnlocks.FirstAsync(
                    item => item.OwnerUserId == unlock.OwnerUserId
                            && item.ContentType == unlock.ContentType
                            && item.ContentId == unlock.ContentId,
                    cancellationToken);
            }
        }

        public async Task<bool> OwnsCardAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            return contentType switch
            {
                ShareContentType.Poll => await _context.Polls
                    .AsNoTracking()
                    .AnyAsync(poll => poll.Id == contentId && poll.CreatorId == ownerId, cancellationToken),
                ShareContentType.Check => await _context.UserChecks
                    .AsNoTracking()
                    .AnyAsync(check => check.Id == contentId && check.UserId == ownerId, cancellationToken),
                _ => false
            };
        }

        public async Task<UserPlanState?> ActivatePlanAsync(
            Guid userId,
            Guid planId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            var plan = await _context.Plans.FirstOrDefaultAsync(item => item.Id == planId, cancellationToken);
            if (user is null || plan is null)
            {
                return null;
            }

            user.ActivatePlan(plan, utcNow);
            await _context.SaveChangesAsync(cancellationToken);
            return await GetUserPlanAsync(userId, cancellationToken);
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        private IQueryable<Plan> PerksQuery()
        {
            return _context.Plans.AsNoTracking();
        }

        private static readonly global::System.Linq.Expressions.Expression<Func<Plan, PlanPerks>> ToPerks =
            plan => new PlanPerks(
                plan.Id,
                plan.Code,
                plan.Name,
                plan.DurationDays,
                plan.UnlockTickets,
                plan.AskBackMessageLimit);

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres
                   && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                   && postgres.ConstraintName == constraintName;
        }
    }
}
