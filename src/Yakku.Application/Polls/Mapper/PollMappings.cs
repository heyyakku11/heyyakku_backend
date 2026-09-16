using Yakku.Application.Polls.DTOs;
using Yakku.Application.Users.DTOs;
using Yakku.Domain.Entities;
using PollEntity = Yakku.Domain.Entities.Poll;

namespace Yakku.Application.Polls.Mapper
{
    internal static class PollMappings
    {
        public static PollResponse ToResponse(this PollEntity poll)
        {
            var totalVotes = poll.TotalVoteCount;
            return new PollResponse
            {
                Id = poll.Id,
                Question = poll.Question,
                ShareToken = poll.ShareToken,
                OptionType = ToOptionTypeString(poll.OptionType),
                ExpiresAt = poll.ExpiresAt,
                TotalVoteCount = totalVotes,
                Options = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new PollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        ImageId = option.ImageId,
                        SecureUrl = option.Image?.SecureUrl,
                        SortOrder = option.SortOrder,
                        VoteCount = option.VoteCount,
                        Percentage = ToPercentage(option.VoteCount, totalVotes)
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
                ShareToken = poll.ShareToken,
                OptionType = ToOptionTypeString(poll.OptionType),
                Status = ToStatusString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                TotalVoteCount = poll.TotalVoteCount
            };
        }

        public static SharedPollResponse ToSharedResponse(this PollEntity poll)
        {
            var totalVotes = poll.TotalVoteCount;
            var effectiveStatus = ToEffectiveShareStatus(poll);
            return new SharedPollResponse
            {
                Question = poll.Question,
                OptionType = ToOptionTypeString(poll.OptionType),
                ExpiresAt = poll.ExpiresAt,
                Status = effectiveStatus,
                IsAcceptingVotes = effectiveStatus == "active",
                Category = poll.Category is null
                    ? null
                    : new SharedPollCategoryResponse
                    {
                        Id = poll.Category.Id,
                        Name = poll.Category.Name
                    },
                Options = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new PollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        ImageId = option.ImageId,
                        SecureUrl = option.Image?.SecureUrl,
                        SortOrder = option.SortOrder,
                        VoteCount = option.VoteCount,
                        Percentage = ToPercentage(option.VoteCount, totalVotes)
                    })
                    .ToList()
            };
        }

        public static UserPollResponse ToUserPollResponse(this PollEntity poll)
        {
            var totalVotes = poll.TotalVoteCount;
            return new UserPollResponse
            {
                PollId = poll.Id,
                Question = poll.Question,
                Status = ToStatusString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                TotalVoteCount = totalVotes,
                PollOptions = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new UserPollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text ?? string.Empty,
                        VoteCount = option.VoteCount,
                        Percentage = ToPercentage(option.VoteCount, totalVotes)
                    })
                    .ToList()
            };
        }

        public static UserPollDetailResponse ToUserPollDetailResponse(
            this PollEntity poll,
            Vote? creatorVote)
        {
            var totalVotes = poll.TotalVoteCount;
            var pollOptions = poll.Options
                .OrderBy(option => option.SortOrder)
                .Select(option => new UserPollDetailOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text,
                    ImageId = option.ImageId,
                    SecureUrl = option.Image?.SecureUrl,
                    SortOrder = option.SortOrder,
                    VoteCount = option.VoteCount,
                    Percentage = ToPercentage(option.VoteCount, totalVotes)
                })
                .ToList();

            var crowdLeader = poll.Options
                .OrderByDescending(option => option.VoteCount)
                .ThenBy(option => option.SortOrder)
                .FirstOrDefault();

            UserPollDetailOptionResponse? crowdLeaderResponse = null;
            if (crowdLeader is not null)
            {
                crowdLeaderResponse = pollOptions.First(option => option.Id == crowdLeader.Id);
            }

            var yourOptionId = creatorVote?.PollOptionId;
            UserPollDetailOptionResponse? yourOptionResponse = null;
            if (yourOptionId is not null)
            {
                yourOptionResponse = pollOptions.FirstOrDefault(option => option.Id == yourOptionId);
            }

            return new UserPollDetailResponse
            {
                PollId = poll.Id,
                Question = poll.Question,
                Status = ToStatusString(poll.Status),
                ShareToken = poll.ShareToken,
                OptionType = ToOptionTypeString(poll.OptionType),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                TotalVoteCount = totalVotes,
                PollOptions = pollOptions,
                YouVsCrowd = new YouVsCrowdResponse
                {
                    YourOptionId = yourOptionId,
                    YourOptionText = yourOptionResponse?.Text,
                    YourOptionPercentage = yourOptionResponse?.Percentage,
                    CrowdLeadingOptionId = crowdLeaderResponse?.Id,
                    CrowdLeadingOptionText = crowdLeaderResponse?.Text,
                    CrowdLeadingPercentage = crowdLeaderResponse?.Percentage,
                    AgreesWithCrowd = yourOptionId is null || crowdLeaderResponse is null
                        ? null
                        : yourOptionId == crowdLeaderResponse.Id
                }
            };
        }

        private static decimal ToPercentage(int voteCount, int totalVotes)
        {
            if (totalVotes <= 0)
            {
                return 0;
            }

            return Math.Round(voteCount * 100m / totalVotes, 2);
        }

        private static string ToOptionTypeString(Domain.Enums.OptionType optionType)
        {
            return optionType.ToString().ToLowerInvariant();
        }

        private static string ToStatusString(Domain.Enums.PollStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        private static string ToEffectiveShareStatus(PollEntity poll)
        {
            if (poll.Status == Domain.Enums.PollStatus.Closed)
            {
                return "closed";
            }

            if (poll.Status == Domain.Enums.PollStatus.Active
                && poll.ExpiresAt is not null
                && poll.ExpiresAt <= DateTime.UtcNow)
            {
                return "expired";
            }

            return ToStatusString(poll.Status);
        }
    }
}
