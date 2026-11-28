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
        private const string OneActiveEntitlementConstraint = "IX_user_plan_entitlements_one_active";

        private readonly YakkuDbContext _context;

        public EntitlementRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<UserPlanState?> GetUserPlanAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new
                {
                    item.PlanActivatedAt,
                    item.PlanExpiresAt,
                    Plan = new PlanPerks(
                        item.Plan.Id,
                        item.Plan.Code,
                        item.Plan.Name,
                        item.Plan.DurationDays,
                        item.Plan.UnlockTickets,
                        item.Plan.AskBackMessageLimit)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return null;
            }

            var entitlement = await _context.UserPlanEntitlements
                .FirstOrDefaultAsync(
                    item => item.UserId == userId && item.Status == UserPlanEntitlementStatus.Active,
                    cancellationToken);

            if (entitlement is not null)
            {
                var plan = await PerksQuery()
                    .Where(item => item.Id == entitlement.PlanId)
                    .Select(ToPerks)
                    .FirstOrDefaultAsync(cancellationToken);

                if (plan is not null)
                {
                    if (entitlement.ExpiresAt <= utcNow)
                    {
                        entitlement.MarkExpired(utcNow);
                        // Keep the user projection's expiry aligned so reason-visibility checks match Ask Back access.
                        await _context.Users
                            .Where(item => item.Id == userId
                                           && item.PlanId == entitlement.PlanId
                                           && (item.PlanExpiresAt == null || item.PlanExpiresAt > entitlement.ExpiresAt))
                            .ExecuteUpdateAsync(
                                setters => setters.SetProperty(item => item.PlanExpiresAt, entitlement.ExpiresAt),
                                cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);

                        return new UserPlanState(plan, entitlement.StartsAt, entitlement.ExpiresAt);
                    }

                    return new UserPlanState(
                        plan,
                        entitlement.StartsAt,
                        entitlement.ExpiresAt,
                        entitlement.Id);
                }
            }

            return new UserPlanState(user.Plan, user.PlanActivatedAt, user.PlanExpiresAt);
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

        public async Task<IReadOnlyList<CardUnlock>> FindUnlocksAsync(
            Guid ownerId,
            IReadOnlyCollection<(ShareContentType ContentType, Guid ContentId)> cards,
            CancellationToken cancellationToken = default)
        {
            if (cards.Count == 0)
            {
                return Array.Empty<CardUnlock>();
            }

            var types = cards.Select(card => card.ContentType).Distinct().ToArray();
            var ids = cards.Select(card => card.ContentId).Distinct().ToArray();
            var candidates = await _context.CardUnlocks
                .Where(unlock => unlock.OwnerUserId == ownerId
                    && types.Contains(unlock.ContentType)
                    && ids.Contains(unlock.ContentId))
                .ToListAsync(cancellationToken);

            var wanted = cards.ToHashSet();
            return candidates
                .Where(unlock => wanted.Contains((unlock.ContentType, unlock.ContentId)))
                .ToList();
        }

        public Task<int> CountPlanUnlocksAsync(
            Guid ownerId,
            Guid? entitlementId,
            DateTime since,
            CancellationToken cancellationToken = default)
        {
            var query = _context.CardUnlocks.AsNoTracking()
                .Where(unlock => unlock.OwnerUserId == ownerId && unlock.Source == CardUnlockSource.Plan);

            if (entitlementId is Guid id)
            {
                query = query.Where(unlock => unlock.UserPlanEntitlementId == id);
            }
            else
            {
                query = query.Where(unlock => unlock.CreatedAt >= since);
            }

            return query.CountAsync(cancellationToken);
        }

        public async Task<CardUnlock?> TryAddPlanUnlockAsync(
            CardUnlock unlock,
            Guid? entitlementId,
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
                var usedQuery = _context.CardUnlocks.Where(item =>
                    item.OwnerUserId == unlock.OwnerUserId && item.Source == CardUnlockSource.Plan);

                usedQuery = entitlementId is Guid id
                    ? usedQuery.Where(item => item.UserPlanEntitlementId == id)
                    : usedQuery.Where(item => item.CreatedAt >= ticketsSince);

                var used = await usedQuery.CountAsync(cancellationToken);
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
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var lockKey = $"user-plan-entitlement:{userId:N}";
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);

            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            var plan = await _context.Plans.FirstOrDefaultAsync(item => item.Id == planId, cancellationToken);
            if (user is null || plan is null)
            {
                return null;
            }

            var actives = await _context.UserPlanEntitlements
                .Where(item => item.UserId == userId && item.Status == UserPlanEntitlementStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var active in actives)
            {
                if (active.ExpiresAt <= utcNow)
                {
                    active.MarkExpired(utcNow);
                }
                else
                {
                    active.MarkSuperseded(utcNow);
                }
            }

            if (plan.Id != Plan.FreeId && plan.DurationDays is { } days && days > 0)
            {
                var created = UserPlanEntitlement.Activate(
                    userId,
                    plan.Id,
                    utcNow,
                    utcNow.AddDays(days),
                    paymentOrderId: null);
                _context.UserPlanEntitlements.Add(created);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception, OneActiveEntitlementConstraint))
                {
                    // Concurrent admin activate: reload and continue with projection update.
                    _context.Entry(created).State = EntityState.Detached;
                    await _context.Entry(user).ReloadAsync(cancellationToken);
                }
            }
            else if (actives.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (plan.Id != Plan.FreeId)
            {
                // Buying a paid plan gives the asker a fresh question budget, as the payment path does.
                await AskBackBudgetReset.ForPlanPurchaseAsync(_context, userId, plan.AskBackMessageLimit, cancellationToken);
            }

            user.ActivatePlan(plan, utcNow);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetUserPlanAsync(userId, utcNow, cancellationToken);
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
