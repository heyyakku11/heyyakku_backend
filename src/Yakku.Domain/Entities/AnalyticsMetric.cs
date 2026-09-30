using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class AnalyticsMetric
    {
        public Guid Id { get; private set; }
        public AnalyticsMetricType MetricType { get; private set; }
        public DateOnly MetricDate { get; private set; }
        public Guid? CategoryId { get; private set; }
        public Guid? UserId { get; private set; }
        public int Value { get; private set; }
        public string? Metadata { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Category? Category { get; private set; }
        public User? User { get; private set; }

        private AnalyticsMetric()
        {
        }

        public AnalyticsMetric(
            AnalyticsMetricType metricType,
            DateOnly metricDate,
            int value = 0,
            Guid? categoryId = null,
            Guid? userId = null,
            string? metadata = null)
        {
            Id = Guid.NewGuid();
            MetricType = metricType;
            MetricDate = metricDate;
            CategoryId = categoryId;
            UserId = userId;
            Value = value;
            Metadata = metadata;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }
    }
}
