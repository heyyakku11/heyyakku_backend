namespace Yakku.Domain.Entities
{
    public class PollAnalytics
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public int TotalVotes { get; private set; }
        public int UniqueVoters { get; private set; }
        public int CommentCount { get; private set; }
        public decimal? CompletionRate { get; private set; }
        public int? AverageTimeToVote { get; private set; }
        public int GuestVotesCount { get; private set; }
        public int UserVotesCount { get; private set; }
        public int Views { get; private set; }
        public int Shares { get; private set; }
        public int ReportCount { get; private set; }
        public DateTime CalculatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;

        private PollAnalytics()
        {
        }

        public PollAnalytics(Guid pollId)
        {
            Id = Guid.NewGuid();
            PollId = pollId;
            TotalVotes = 0;
            UniqueVoters = 0;
            CommentCount = 0;
            GuestVotesCount = 0;
            UserVotesCount = 0;
            Views = 0;
            Shares = 0;
            ReportCount = 0;
            CalculatedAt = DateTime.UtcNow;
            UpdatedAt = CalculatedAt;
        }
    }
}
