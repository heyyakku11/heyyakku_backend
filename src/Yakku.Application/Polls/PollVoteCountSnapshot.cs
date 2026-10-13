namespace Yakku.Application.Polls
{
    public sealed class PollVoteCountSnapshot
    {
        public static PollVoteCountSnapshot Empty { get; } = new(
            0,
            0,
            0,
            new Dictionary<Guid, int>());

        public int TotalVotes { get; }
        public int WhyCount { get; }
        public int CustomAnswerCount { get; }
        public IReadOnlyDictionary<Guid, int> OptionCounts { get; }

        public PollVoteCountSnapshot(
            int totalVotes,
            int whyCount,
            int customAnswerCount,
            IReadOnlyDictionary<Guid, int> optionCounts)
        {
            TotalVotes = totalVotes;
            WhyCount = whyCount;
            CustomAnswerCount = customAnswerCount;
            OptionCounts = optionCounts;
        }

        public int CountFor(Guid optionId)
        {
            return OptionCounts.TryGetValue(optionId, out var count) ? count : 0;
        }

        public static IReadOnlyDictionary<Guid, PollVoteCountSnapshot> Combine(
            IEnumerable<PollVoteCountRow> rows)
        {
            var grouped = new Dictionary<Guid, List<PollVoteCountRow>>();
            foreach (var row in rows)
            {
                if (!grouped.TryGetValue(row.PollId, out var list))
                {
                    list = [];
                    grouped[row.PollId] = list;
                }

                list.Add(row);
            }

            var result = new Dictionary<Guid, PollVoteCountSnapshot>(grouped.Count);
            foreach (var (pollId, pollRows) in grouped)
            {
                var optionCounts = new Dictionary<Guid, int>();
                var total = 0;
                var why = 0;
                var custom = 0;
                foreach (var row in pollRows)
                {
                    total += row.Count;
                    why += row.WhyCount;
                    if (row.PollOptionId is Guid optionId)
                    {
                        optionCounts[optionId] = row.Count;
                    }
                    else
                    {
                        custom += row.Count;
                    }
                }

                result[pollId] = new PollVoteCountSnapshot(total, why, custom, optionCounts);
            }

            return result;
        }
    }

    public readonly record struct PollVoteCountRow(
        Guid PollId,
        Guid? PollOptionId,
        int Count,
        int WhyCount);
}
