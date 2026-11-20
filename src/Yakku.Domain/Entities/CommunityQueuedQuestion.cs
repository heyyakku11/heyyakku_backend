namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A question the user queued for Community before it opens. Each user may hold at most
    /// <see cref="MaxPerUser"/>; category tags live on <see cref="CardTag"/>.
    /// </summary>
    public class CommunityQueuedQuestion
    {
        public const int MaxPerUser = 3;
        public const int QuestionMaxLength = 280;

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string Question { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;
        public ICollection<CardTag> Tags { get; private set; } = new List<CardTag>();

        private CommunityQueuedQuestion()
        {
        }

        public static CommunityQueuedQuestion Create(Guid userId, string question, IEnumerable<Guid> categoryIds)
        {
            var now = DateTime.UtcNow;
            var entity = new CommunityQueuedQuestion
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Question = question.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            entity.ReplaceTags(categoryIds);
            return entity;
        }

        public void Update(string question, IEnumerable<Guid> categoryIds)
        {
            Question = question.Trim();
            ReplaceTags(categoryIds);
            UpdatedAt = DateTime.UtcNow;
        }

        private void ReplaceTags(IEnumerable<Guid> categoryIds)
        {
            Tags.Clear();
            foreach (var categoryId in categoryIds.Distinct())
            {
                Tags.Add(CardTag.Create(Id, categoryId));
            }
        }
    }
}
