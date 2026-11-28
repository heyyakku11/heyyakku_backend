namespace Yakku.Application.Plans.DTOs
{
    public class PlanResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Price in the smallest currency unit (paise for INR).</summary>
        public int PriceMinor { get; set; }
        public string Currency { get; set; } = string.Empty;

        /// <summary>How long one activation lasts in days; null when the plan never expires (free).</summary>
        public int? DurationDays { get; set; }

        /// <summary>Cards the holder can unlock for Ask Back per activation; null means unlimited.</summary>
        public int? UnlockTickets { get; set; }

        /// <summary>Questions the asker can send per reason; null means unlimited.</summary>
        public int? AskBackMessageLimit { get; set; }
    }

    /// <summary>Only the fields that are sent are changed.</summary>
    public class UpdatePlanRequest
    {
        public string? Name { get; set; }
        public int? PriceMinor { get; set; }
        public bool? IsActive { get; set; }

        /// <summary>Days an activation lasts (1 or more).</summary>
        public int? DurationDays { get; set; }

        /// <summary>Unlock tickets per activation: 0 or more, or -1 for unlimited.</summary>
        public int? UnlockTickets { get; set; }

        /// <summary>Questions per reason: 0 or more, or -1 for unlimited.</summary>
        public int? AskBackMessageLimit { get; set; }
    }
}
