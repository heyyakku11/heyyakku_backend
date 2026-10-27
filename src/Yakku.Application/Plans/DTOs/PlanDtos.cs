namespace Yakku.Application.Plans.DTOs
{
    public class PlanResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Price in the smallest currency unit (paise for INR).</summary>
        public int PriceMinor { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    /// <summary>Only the fields that are sent are changed.</summary>
    public class UpdatePlanRequest
    {
        public string? Name { get; set; }
        public int? PriceMinor { get; set; }
        public bool? IsActive { get; set; }
    }
}
