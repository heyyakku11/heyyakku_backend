using System.Text.Json;
using System.Text.Json.Serialization;
using Yakku.Domain.Entities;

namespace Yakku.Application.Payments
{
    /// <summary>Plan fields frozen onto a payment order so later catalog edits do not rewrite the purchase.</summary>
    public sealed class PlanPurchaseSnapshot
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int PriceMinor { get; init; }
        public string Currency { get; init; } = "INR";
        public int? DurationDays { get; init; }
        public int? UnlockTickets { get; init; }
        public int? AskBackMessageLimit { get; init; }

        public static PlanPurchaseSnapshot FromPlan(Plan plan)
        {
            return new PlanPurchaseSnapshot
            {
                Id = plan.Id,
                Code = plan.Code,
                Name = plan.Name,
                PriceMinor = plan.PriceMinor,
                Currency = plan.Currency,
                DurationDays = plan.DurationDays,
                UnlockTickets = plan.UnlockTickets,
                AskBackMessageLimit = plan.AskBackMessageLimit
            };
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }
    }
}
