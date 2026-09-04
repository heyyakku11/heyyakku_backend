namespace Yakku.Domain.Entities
{
    public class PollOptions
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public int Position { get; private set; }
        public Polls Poll { get; private set; } = null!;

        private PollOptions()
        {
        }

        public PollOptions(Guid pollId, string text, int position)
        {
            Id = Guid.NewGuid();
            PollId = pollId;
            Text = text;
            Position = position;
        }
    }
}
