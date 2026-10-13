using Yakku.Application.Votes.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Votes.Mapper
{
    internal static class VoteMappings
    {
        public static VoteResponse ToResponse(this PollVote vote)
        {
            return new VoteResponse
            {
                Id = vote.Id,
                PollId = vote.PollId,
                PollOptionId = vote.PollOptionId,
                CustomAnswer = vote.CustomAnswer,
                Reason = vote.Reason,
                CreatedAt = vote.CreatedAt
            };
        }
    }
}
