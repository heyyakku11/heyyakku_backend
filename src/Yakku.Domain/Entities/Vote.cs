namespace Yakku.Domain.Entities
{
    public class Vote
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? GuestId { get; private set; }
        public Guid? PollOptionId { get; private set; }
        public string? CustomOptionText { get; private set; }
        public string? Reason { get; private set; }
        public Guid? ImageId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Polls Poll { get; private set; } = null!;
        public User? User { get; private set; }
        public Guest? Guest { get; private set; }
        public PollOptions? PollOption { get; private set; }
        public Image? Image { get; private set; }

        private Vote()
        {
        }

        public Vote(
            Guid guestId,
            Guid pollId,
            Guid? pollOptionId,
            string? customOptionText,
            string? reason,
            Guid? imageId = null)
        {
            Id = Guid.NewGuid();
            GuestId = guestId;
            PollId = pollId;
            PollOptionId = pollOptionId;
            CustomOptionText = customOptionText;
            Reason = reason;
            ImageId = imageId;
            CreatedAt = DateTime.UtcNow;
        }

        public static Vote ForUser(
            Guid userId,
            Guid pollId,
            Guid? pollOptionId,
            string? customOptionText,
            string? reason,
            Guid? imageId = null)
        {
            return new Vote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PollId = pollId,
                PollOptionId = pollOptionId,
                CustomOptionText = customOptionText,
                Reason = reason,
                ImageId = imageId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
