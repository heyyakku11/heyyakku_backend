namespace Yakku.Domain.Entities
{
    /// <summary>One category tag on a queued community question (FK to <see cref="CardCategory"/>).</summary>
    public class CardTag
    {
        public Guid QuestionId { get; private set; }
        public Guid CategoryId { get; private set; }

        public CommunityQueuedQuestion Question { get; private set; } = null!;
        public CardCategory Category { get; private set; } = null!;

        private CardTag()
        {
        }

        public static CardTag Create(Guid questionId, Guid categoryId)
        {
            return new CardTag
            {
                QuestionId = questionId,
                CategoryId = categoryId
            };
        }
    }
}
