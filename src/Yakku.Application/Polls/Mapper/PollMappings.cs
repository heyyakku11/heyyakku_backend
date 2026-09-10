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
                Question = poll.Question,
                CategoryId = poll.CategoryId,
                ShareToken = poll.ShareToken,
                OptionType = ToOptionTypeString(poll.OptionType),
                Status = ToStatusString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                ClosedAt = poll.ClosedAt,
                CreatedAt = poll.CreatedAt,
                Options = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new PollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        ImageId = option.ImageId,
                        SecureUrl = option.Image?.SecureUrl,
                        SortOrder = option.SortOrder
                    })
                    .ToList()
            };
        }

        public static PollSummaryResponse ToSummaryResponse(this PollEntity poll)
        {
            return new PollSummaryResponse
            {
                Id = poll.Id,
                Question = poll.Question,
                CategoryId = poll.CategoryId,
                OptionType = ToOptionTypeString(poll.OptionType),
                Status = ToStatusString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt
            };
        }

        public static UserPollResponse ToUserPollResponse(this PollEntity poll)
        {
            return new UserPollResponse
            {
                PollId = poll.Id,
                Question = poll.Question,
                Status = ToStatusString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                PollOptions = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new UserPollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text ?? string.Empty
                    })
                    .ToList()
            };
        }

        private static string ToOptionTypeString(Domain.Enums.OptionType optionType)
        {
            return optionType.ToString().ToLowerInvariant();
        }

        private static string ToStatusString(Domain.Enums.PollStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }
    }
}
