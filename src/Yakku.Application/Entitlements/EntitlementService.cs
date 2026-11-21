using Yakku.Application.AskBack;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Entitlements
{
    public class EntitlementService : IEntitlementService
    {
        private readonly IEntitlementRepository _repository;
        private readonly IAskBackRepository _askBack;
        private readonly TimeProvider _time;

        public EntitlementService(
            IEntitlementRepository repository,
            IAskBackRepository askBack,
            TimeProvider time)
        {
            _repository = repository;
            _askBack = askBack;
            _time = time;
        }

        public async Task<CardAccess> GetCardAccessAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            var batch = await GetCardAccessBatchAsync(
                ownerId,
                [(contentType, contentId)],
                cancellationToken);
            return batch[(contentType, contentId)];
        }

        public async Task<IReadOnlyDictionary<(ShareContentType ContentType, Guid ContentId), CardAccess>> GetCardAccessBatchAsync(
            Guid ownerId,
            IReadOnlyCollection<(ShareContentType ContentType, Guid ContentId)> cards,
            CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var plan = await ResolvePlanAsync(ownerId, now, cancellationToken);
            var purchaseLimit = await GetPurchaseMessageLimitAsync(cancellationToken);

            int? ticketsLeft = null;
            var canUnlockWithPlan = false;
            var canAutoUnlock = false;
            if (plan.IsPaid)
            {
                if (plan.Plan.UnlockTickets is not { } tickets)
                {
                    canUnlockWithPlan = true;
                    canAutoUnlock = true;
                }
                else
                {
                    var used = await _repository.CountPlanUnlocksAsync(
                        ownerId,
                        plan.EntitlementId,
                        plan.ActivatedAt ?? DateTime.MinValue,
                        cancellationToken);
                    ticketsLeft = Math.Max(0, tickets - used);
                    canUnlockWithPlan = ticketsLeft > 0;
                }
            }

            var unlocks = cards.Count == 0
                ? Array.Empty<CardUnlock>()
                : await _repository.FindUnlocksAsync(ownerId, cards, cancellationToken);
            var unlockByCard = unlocks.ToDictionary(
                unlock => (unlock.ContentType, unlock.ContentId));

            var result = new Dictionary<(ShareContentType ContentType, Guid ContentId), CardAccess>();
            foreach (var card in cards.Distinct())
            {
                unlockByCard.TryGetValue(card, out var unlock);
                int? limit;
                var paused = false;
                if (unlock is null)
                {
                    limit = purchaseLimit;
                }
                else if (unlock.Source == CardUnlockSource.Plan)
                {
                    paused = !plan.IsPaid;
                    limit = plan.IsPaid ? plan.Plan.AskBackMessageLimit : purchaseLimit;
                }
                else
                {
                    limit = plan.IsPaid ? MoreGenerous(purchaseLimit, plan.Plan.AskBackMessageLimit) : purchaseLimit;
                }

                result[card] = new CardAccess(
                    unlock is not null,
                    unlock?.Source,
                    paused,
                    limit,
                    purchaseLimit,
                    ticketsLeft,
                    canUnlockWithPlan,
                    canAutoUnlock,
                    plan);
            }

            return result;
        }

        public async Task<bool> TryAutoUnlockAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            var access = await GetCardAccessAsync(ownerId, contentType, contentId, cancellationToken);
            if (access.Unlocked)
            {
                return !access.Paused;
            }

            if (!access.CanAutoUnlock)
            {
                return false;
            }

            var unlock = CardUnlock.Create(
                ownerId,
                contentType,
                contentId,
                CardUnlockSource.Plan,
                _time.GetUtcNow().UtcDateTime,
                userPlanEntitlementId: access.Plan.EntitlementId);
            return await _repository.TryAddPlanUnlockAsync(
                unlock,
                access.Plan.EntitlementId,
                access.Plan.ActivatedAt ?? DateTime.MinValue,
                tickets: null,
                cancellationToken) is not null;
        }

        public async Task<AskBackAccessResponse> GetAccessAsync(
            Guid userId,
            Guid voteId,
            CancellationToken cancellationToken = default)
        {
            var vote = await FindOwnedVoteAsync(userId, voteId, cancellationToken);
            var access = await GetCardAccessAsync(userId, vote.ContentType, vote.ContentId, cancellationToken);
            return ToResponse(access);
        }

        public async Task<AskBackAccessResponse> GetOwnedCardAccessAsync(
            Guid userId,
            string? contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            var type = ParseContentType(contentType);
            if (!await _repository.OwnsCardAsync(userId, type, contentId, cancellationToken))
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Card not found");
            }

            return ToResponse(await GetCardAccessAsync(userId, type, contentId, cancellationToken));
        }

        public async Task<AskBackAccessResponse> UnlockAsync(
            Guid userId,
            Guid voteId,
            CancellationToken cancellationToken = default)
        {
            var vote = await FindOwnedVoteAsync(userId, voteId, cancellationToken);
            var access = await GetCardAccessAsync(userId, vote.ContentType, vote.ContentId, cancellationToken);
            if (access.Unlocked && !access.Paused)
            {
                return ToResponse(access);
            }

            if (!access.CanUnlockWithPlan)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.AskBackUnlockRequired,
                    access.Plan.IsPaid
                        ? "You have used every unlock ticket on your plan."
                        : "Unlock this card to ask back on every reason.");
            }

            var unlock = CardUnlock.Create(
                userId,
                vote.ContentType,
                vote.ContentId,
                CardUnlockSource.Plan,
                _time.GetUtcNow().UtcDateTime,
                userPlanEntitlementId: access.Plan.EntitlementId);
            var saved = await _repository.TryAddPlanUnlockAsync(
                unlock,
                access.Plan.EntitlementId,
                access.Plan.ActivatedAt ?? DateTime.MinValue,
                access.Plan.Plan.UnlockTickets,
                cancellationToken);
            if (saved is null)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.AskBackUnlockRequired,
                    "You have used every unlock ticket on your plan.");
            }

            return ToResponse(await GetCardAccessAsync(userId, vote.ContentType, vote.ContentId, cancellationToken));
        }

        public async Task<ActivatePlanResponse> ActivatePlanAsync(
            Guid userId,
            string? planCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(planCode))
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "planCode is required.", "planCode");
            }

            var code = planCode.Trim().ToLowerInvariant();
            if (code == Plan.ThisCardCode)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This card is a one-off unlock, not an activatable plan.",
                    "planCode");
            }

            var plan = await _repository.GetPlanByCodeAsync(code, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Plan not found");
            var state = await _repository.ActivatePlanAsync(
                userId, plan.Id, _time.GetUtcNow().UtcDateTime, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "User not found");

            return new ActivatePlanResponse
            {
                UserId = userId,
                PlanCode = state.Plan.Code,
                ActivatedAt = state.ActivatedAt,
                ExpiresAt = state.ExpiresAt
            };
        }

        public async Task<GrantCardUnlockResponse> GrantCardUnlockAsync(
            Guid userId,
            string? contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            var type = ParseContentType(contentType);
            if (!await _repository.OwnsCardAsync(userId, type, contentId, cancellationToken))
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Card not found");
            }

            var existing = await _repository.FindUnlockAsync(userId, type, contentId, cancellationToken);
            if (existing is null)
            {
                existing = await _repository.AddUnlockAsync(
                    CardUnlock.Create(
                        userId, type, contentId, CardUnlockSource.Purchase, _time.GetUtcNow().UtcDateTime),
                    cancellationToken);
            }

            if (existing.PromoteToPurchase())
            {
                await _repository.SaveAsync(cancellationToken);
            }

            return new GrantCardUnlockResponse
            {
                UserId = userId,
                ContentType = type == ShareContentType.Poll ? "poll" : "check",
                ContentId = contentId,
                Source = "purchase"
            };
        }

        public async Task<int?> GetPurchaseMessageLimitAsync(CancellationToken cancellationToken = default)
        {
            var free = await _repository.GetPlanByIdAsync(Plan.FreeId, cancellationToken);
            return free is null ? AskBackOptions.FallbackMessageLimit : free.AskBackMessageLimit;
        }

        /// <summary>The plan that counts for the user now. A paid plan that has ended counts as free.</summary>
        private async Task<EffectivePlan> ResolvePlanAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
        {
            var state = await _repository.GetUserPlanAsync(userId, now, cancellationToken);
            if (state is not null && state.Plan.Id != Plan.FreeId && (state.ExpiresAt is null || state.ExpiresAt > now))
            {
                return new EffectivePlan(
                    state.Plan, true, false, state.ActivatedAt, state.ExpiresAt, state.EntitlementId);
            }

            var free = await _repository.GetPlanByIdAsync(Plan.FreeId, cancellationToken)
                ?? new PlanPerks(Plan.FreeId, Plan.FreeCode, "Free", null, 0, AskBackOptions.FallbackMessageLimit);
            var expired = state is not null && state.Plan.Id != Plan.FreeId;
            return new EffectivePlan(free, false, expired, state?.ActivatedAt, state?.ExpiresAt);
        }

        /// <summary>The vote's context, only for the owner of its card and only once the reasons are revealed.</summary>
        private async Task<AskBackVoteContext> FindOwnedVoteAsync(
            Guid userId,
            Guid voteId,
            CancellationToken cancellationToken)
        {
            var vote = await _askBack.FindVoteContextAsync(voteId, cancellationToken);
            if (vote is null
                || vote.OwnerId != userId
                || vote.OtherVotes < OperationalSettings.Results.MinVotesToReveal)
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Vote not found.");
            }

            return vote;
        }

        private static AskBackAccessResponse ToResponse(CardAccess access)
        {
            // Unlocked: the limit of the conversations that exist. Locked: what unlocking would give.
            var limit = access.Unlocked
                ? access.MessageLimit
                : access.CanUnlockWithPlan ? access.Plan.Plan.AskBackMessageLimit : access.PurchaseMessageLimit;

            return new AskBackAccessResponse
            {
                Unlocked = access.Unlocked,
                ReasonsRevealed = access.ReasonsRevealed,
                UnlockSource = access.Source?.ToString().ToLowerInvariant(),
                Paused = access.Paused,
                PlanCode = access.Plan.Plan.Code,
                PlanName = access.Plan.Plan.Name,
                PlanExpiresAt = access.Plan.IsPaid || access.Plan.Expired ? access.Plan.ExpiresAt : null,
                PlanExpired = access.Plan.Expired,
                TicketsLeft = access.TicketsLeft,
                CanUnlock = access.CanUnlockWithPlan,
                MessageLimit = limit,
                UnlimitedMessages = limit is null
            };
        }

        private static int? MoreGenerous(int? a, int? b)
        {
            if (a is null || b is null)
            {
                return null;
            }

            return Math.Max(a.Value, b.Value);
        }

        private static ShareContentType ParseContentType(string? contentType)
        {
            return contentType?.Trim().ToLowerInvariant() switch
            {
                "poll" => ShareContentType.Poll,
                "check" => ShareContentType.Check,
                _ => throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "contentType must be poll or check.",
                    "contentType")
            };
        }
    }
}
