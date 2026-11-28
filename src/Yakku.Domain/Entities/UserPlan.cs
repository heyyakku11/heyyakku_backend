namespace Yakku.Domain.Entities
{
    /// <summary>
    /// The plan a user is on, one row per user (1:1 with <see cref="User"/>, keyed by the user id). The row is created
    /// together with the account on the free plan, so every user has one. Only yakku_lite and yakku_love ever expire.
    /// </summary>
    public class UserPlan
    {
        public Guid UserId { get; private set; }

        /// <summary>The code of the plan the user is on (<see cref="Plan.Code"/>). New users start on <see cref="Plan.FreeCode"/>.</summary>
        public string PlanCode { get; private set; } = Plan.FreeCode;

        /// <summary>When the current paid plan was activated. Null on the free plan.</summary>
        public DateTime? PlanActivatedAt { get; private set; }

        /// <summary>When the current paid plan ends. Null on the free plan. After it, the user counts as free.</summary>
        public DateTime? PlanExpiresAt { get; private set; }

        public User User { get; private set; } = null!;

        public Plan Plan { get; private set; } = null!;

        private UserPlan()
        {
        }

        /// <summary>A new row on the free plan.</summary>
        public UserPlan(Guid userId)
        {
            UserId = userId;
            PlanCode = Plan.FreeCode;
        }

        /// <summary>
        /// Puts the user on <paramref name="plan"/> from <paramref name="utcNow"/>. A plan with a duration ends that many
        /// days later; activating again restarts the period (and the tickets that count from the activation time).
        /// </summary>
        public void Activate(Plan plan, DateTime utcNow)
        {
            PlanCode = plan.Code;
            Plan = plan;

            if (plan.DurationDays is { } days)
            {
                PlanActivatedAt = utcNow;
                PlanExpiresAt = utcNow.AddDays(days);
            }
            else
            {
                PlanActivatedAt = null;
                PlanExpiresAt = null;
            }
        }

        /// <summary>
        /// False once a paid plan's end has passed. The free plan never expires, and neither does a paid plan that
        /// has no duration. Callers treat an inactive paid plan as the free plan.
        /// </summary>
        public bool IsActive(DateTime utcNow)
        {
            return PlanCode == Plan.FreeCode || PlanExpiresAt is null || PlanExpiresAt > utcNow;
        }
    }
}
