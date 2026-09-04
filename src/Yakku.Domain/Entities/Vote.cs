namespace Yakku.Domain.Entities
{
    public class Vote
    {
        public Guid Id { get; private set; }
        public Guid GuestId { get; private set; }
        public Guid PollId { get; private set; }
        public Guid? PollOptionId { get; private set; }
        public string? CustomOptionText { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private Vote()
        {
        }

        public Vote(
            Guid guestId,
            Guid pollId,
            Guid? pollOptionId,
            string? customOptionText,
            string? reason)
        {
            Id = Guid.NewGuid();
            GuestId = guestId;
            PollId = pollId;
            PollOptionId = pollOptionId;
            CustomOptionText = customOptionText;
            Reason = reason;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
