namespace Yakku.Domain.Entities
{
    public class Category
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Slug { get; private set; } = string.Empty;
        public string? Icon { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public Guid? DeletedBy { get; private set; }

        public Admin? DeletedByAdmin { get; private set; }
        public ICollection<PollCategory> PollCategories { get; private set; } = new List<PollCategory>();
        public ICollection<AnalyticsMetric> AnalyticsMetrics { get; private set; } = new List<AnalyticsMetric>();

        private Category()
        {
        }

        public Category(string name, string slug)
        {
            Id = Guid.NewGuid();
            Name = name;
            Slug = slug;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }
    }
}
