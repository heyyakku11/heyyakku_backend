using Yakku.Application.Votes.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Votes.Mapper
{
    internal static class VoteMappings
    {
        public static VoteResponse ToResponse(this Vote vote)
        {
            return new VoteResponse
            {
                Id = vote.Id,
                PollId = vote.PollId,
                PollOptionId = vote.PollOptionId,
                CustomOptionText = vote.CustomOptionText,
                Reason = vote.Reason,
                CreatedAt = vote.CreatedAt
            };
        }
    }
}
