using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Polls
{
    public sealed class AnsweredPollEntry
    {
        public required PollEntity Poll { get; init; }
        public required DateTime VotedAt { get; init; }
    }
}
