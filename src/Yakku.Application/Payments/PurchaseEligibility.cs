using Yakku.Domain.Entities;

namespace Yakku.Application.Payments
{
    /// <summary>Pure purchase-rule checks for Lite / Love / this_card (Rules A–F minus guest auth).</summary>
    public static class PurchaseEligibility
    {
        public sealed record EffectivePaidPlan(Guid PlanId, string Code, DateTime? ExpiresAt);

        /// <summary>
        /// The paid plan that currently counts. Expired paid projections fall back to free (null).
        /// </summary>
        public static EffectivePaidPlan? ResolveEffectivePaidPlan(
            Guid planId,
            DateTime? expiresAt,
            DateTime utcNow)
        {
            if (planId == Plan.FreeId || planId == Plan.ThisCardId)
            {
                return null;
            }

            if (expiresAt is { } end && end <= utcNow)
            {
                return null;
            }

            var code = planId == Plan.YakkuLiteId
                ? Plan.YakkuLiteCode
                : planId == Plan.YakkuLoveId
                    ? Plan.YakkuLoveCode
                    : string.Empty;

            if (code.Length == 0)
            {
                return null;
            }

            return new EffectivePaidPlan(planId, code, expiresAt);
        }

        public static string? RejectUserPlanPurchase(EffectivePaidPlan? current, string requestedCode)
        {
            if (requestedCode is not (Plan.YakkuLiteCode or Plan.YakkuLoveCode))
            {
                return "Only yakku_lite or yakku_love can be purchased as a user plan.";
            }

            if (current is null)
            {
                return null;
            }

            if (current.Code == requestedCode)
            {
                return requestedCode == Plan.YakkuLiteCode
                    ? "You already have an active Yakku Lite plan."
                    : "You already have an active Yakku Love plan.";
            }

            if (current.Code == Plan.YakkuLoveCode && requestedCode == Plan.YakkuLiteCode)
            {
                return "You cannot downgrade from Yakku Love to Yakku Lite while Love is active.";
            }

            // Lite → Love is allowed.
            return null;
        }
    }
}
