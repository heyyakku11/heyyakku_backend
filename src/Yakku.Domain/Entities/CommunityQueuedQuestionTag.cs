namespace Yakku.Domain.Entities
{
    /// <summary>One category tag on a queued community question (allow-listed codes).</summary>
    public class CommunityQueuedQuestionTag
    {
        public Guid QuestionId { get; private set; }
        public string Tag { get; private set; } = string.Empty;

        public CommunityQueuedQuestion Question { get; private set; } = null!;

        private CommunityQueuedQuestionTag()
        {
        }

        public static CommunityQueuedQuestionTag Create(Guid questionId, string tag)
        {
            return new CommunityQueuedQuestionTag
            {
                QuestionId = questionId,
                Tag = tag
            };
        }
    }
}
