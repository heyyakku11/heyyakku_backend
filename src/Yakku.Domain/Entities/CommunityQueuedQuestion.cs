namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A question the user queued for Community before it opens. Each user may hold at most
    /// <see cref="MaxPerUser"/>; category tags live on <see cref="CommunityQueuedQuestionTag"/>.
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
        public ICollection<CommunityQueuedQuestionTag> Tags { get; private set; } = new List<CommunityQueuedQuestionTag>();

        private CommunityQueuedQuestion()
        {
        }

        public static CommunityQueuedQuestion Create(Guid userId, string question, IEnumerable<string> tags)
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
            entity.ReplaceTags(tags);
            return entity;
        }

        public void Update(string question, IEnumerable<string> tags)
        {
            Question = question.Trim();
            ReplaceTags(tags);
            UpdatedAt = DateTime.UtcNow;
        }

        private void ReplaceTags(IEnumerable<string> tags)
        {
            Tags.Clear();
            foreach (var tag in tags
                         .Select(CommunityQueuedQuestionCategories.Normalize)
                         .Where(tag => tag is not null)
                         .Cast<string>()
                         .Distinct(StringComparer.Ordinal))
            {
                Tags.Add(CommunityQueuedQuestionTag.Create(Id, tag));
            }
        }
    }
}
