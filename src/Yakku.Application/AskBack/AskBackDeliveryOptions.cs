namespace Yakku.Application.AskBack
{
    /// <summary>
    /// Real-time delivery tracking. Bound from the "AskBackDelivery" section (or AskBackDelivery__* environment
    /// variables) and validated when the host starts.
    /// </summary>
    public sealed class AskBackDeliveryOptions
    {
        public const string SectionName = "AskBackDelivery";

        /// <summary>A Sent delivery with no acknowledgment after this long is marked TimedOut.</summary>
        public int AckTimeoutSeconds { get; set; } = 30;

        /// <summary>How often the sweeper looks for deliveries past the timeout.</summary>
        public int SweepIntervalSeconds { get; set; } = 15;

        /// <summary>Rows the sweeper handles per query.</summary>
        public int SweepBatchSize { get; set; } = 100;

        public TimeSpan AckTimeout => TimeSpan.FromSeconds(AckTimeoutSeconds);

        public TimeSpan SweepInterval => TimeSpan.FromSeconds(SweepIntervalSeconds);

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (AckTimeoutSeconds is < 1 or > 86_400)
            {
                errors.Add($"{SectionName}:{nameof(AckTimeoutSeconds)} must be between 1 and 86400.");
            }

            if (SweepIntervalSeconds is < 1 or > 3_600)
            {
                errors.Add($"{SectionName}:{nameof(SweepIntervalSeconds)} must be between 1 and 3600.");
            }

            if (SweepBatchSize is < 1 or > 10_000)
            {
                errors.Add($"{SectionName}:{nameof(SweepBatchSize)} must be between 1 and 10000.");
            }

            return errors;
        }
    }
}
