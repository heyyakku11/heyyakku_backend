using Yakku.Application.Polls.DTOs;
using Yakku.Application.Users.DTOs;
using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Polls.Mapper
{
    internal static class PollMappings
    {
        public static PollResponse ToResponse(this PollEntity poll)
        {
            return new PollResponse
            {
                Id = poll.Id,
                CreatorId = poll.CreatorId,
                Question = poll.Question,
                Status = poll.Status.ToString(),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                Options = poll.Options
                    .OrderBy(option => option.Position)
                    .Select(option => new PollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        IsSystemOption = false
                    })
                    .Append(new PollOptionResponse
                    {
                        Id = null,
                        Text = "Something else",
                        IsSystemOption = true
                    })
                    .ToList()
            };
        }

        public static CreatePollResponse ToCreateResponse(this PollEntity poll)
        {
            return new CreatePollResponse
            {
                Id = poll.Id,
                CreatorId = poll.CreatorId,
                Question = poll.Question
            };
        }

        public static UserPollResponse ToUserPollResponse(this PollEntity poll)
        {
            return new UserPollResponse
            {
                PollId = poll.Id,
                Question = poll.Question,
                Status = poll.Status.ToString(),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                PollOptions = poll.Options
                    .OrderBy(option => option.Position)
                    .Select(option => new UserPollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text
                    })
                    .ToList()
            };
        }
    }
}
