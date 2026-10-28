using Yakku.Application.Polls.DTOs;
using Yakku.Application.Users.DTOs;
using Yakku.Domain.Entities;
using PollEntity = Yakku.Domain.Entities.Poll;

namespace Yakku.Application.Polls.Mapper
{
    internal static class PollMappings
    {
        public static PollResponse ToResponse(this PollEntity poll, PollVoteCountSnapshot snapshot)
        {
            var counts = snapshot.ForDisplay();
            return new PollResponse
            {
                Id = poll.Id,
                ShareCode = poll.ShareCode,
                Question = poll.Question,
                ContentType = ToEnumString(poll.ContentType),
                Status = ToEnumString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                MediaReadiness = poll.Media is null ? "none" : "ready",
                Media = poll.Media is null ? null : ToMediaResponse(poll.Media),
                TotalVoteCount = counts.TotalVotes,
                MinVotes = counts.MinVotes,
                VoteProgress = counts.OtherVotes,
                ResultsUnlocked = counts.ResultsUnlocked,
                CustomAnswerCount = counts.CustomAnswerCount,
                TotalWhyCount = counts.WhyCount,
                Options = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => ToOptionResponse(option, counts))
                    .ToList()
            };
        }

        public static PollSummaryResponse ToSummaryResponse(this PollEntity poll, PollVoteCountSnapshot snapshot)
        {
            var counts = snapshot.ForDisplay();
            return new PollSummaryResponse
            {
                Id = poll.Id,
                ShareCode = poll.ShareCode,
                Question = poll.Question,
                ContentType = ToEnumString(poll.ContentType),
                Status = ToEnumString(poll.Status),
                CreatedAt = poll.CreatedAt,
                TotalVoteCount = counts.TotalVotes,
                MinVotes = counts.MinVotes,
                VoteProgress = counts.OtherVotes,
                ResultsUnlocked = counts.ResultsUnlocked,
                CustomAnswerCount = counts.CustomAnswerCount,
                TotalWhyCount = counts.WhyCount
            };
        }

        public static UserPollResponse ToUserPollResponse(this PollEntity poll, PollVoteCountSnapshot snapshot)
        {
            var counts = snapshot.ForDisplay();
            return new UserPollResponse
            {
                PollId = poll.Id,
                ShareCode = poll.ShareCode,
                Question = poll.Question,
                ContentType = ToEnumString(poll.ContentType),
                Status = ToEnumString(poll.Status),
                CreatedAt = poll.CreatedAt,
                TotalVoteCount = counts.TotalVotes,
                MinVotes = counts.MinVotes,
                VoteProgress = counts.OtherVotes,
                ResultsUnlocked = counts.ResultsUnlocked,
                CustomAnswerCount = counts.CustomAnswerCount,
                TotalWhyCount = counts.WhyCount,
                PollOptions = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new UserPollOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        VoteCount = counts.CountFor(option.Id),
                        Percentage = ToPercentage(counts.CountFor(option.Id), counts.TotalVotes)
                    })
                    .ToList()
            };
        }

        public static UserPollDetailResponse ToUserPollDetailResponse(
            this PollEntity poll,
            PollVote? creatorVote,
            PollVoteCountSnapshot snapshot)
        {
            var counts = snapshot.ForDisplay();
            var pollOptions = poll.Options
                .OrderBy(option => option.SortOrder)
                .Select(option => new UserPollDetailOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text,
                    SortOrder = option.SortOrder,
                    VoteCount = counts.CountFor(option.Id),
                    Percentage = ToPercentage(counts.CountFor(option.Id), counts.TotalVotes),
                    WhyCount = counts.WhyCountFor(option.Id)
                })
                .ToList();

            var crowdLeader = pollOptions
                .OrderByDescending(option => option.VoteCount)
                .ThenBy(option => option.SortOrder)
                .FirstOrDefault();

            var yourOptionId = creatorVote?.PollOptionId;
            UserPollDetailOptionResponse? yourOptionResponse = null;
            if (yourOptionId is not null)
            {
                yourOptionResponse = pollOptions.FirstOrDefault(option => option.Id == yourOptionId);
            }

            return new UserPollDetailResponse
            {
                PollId = poll.Id,
                ShareCode = poll.ShareCode,
                Question = poll.Question,
                ContentType = ToEnumString(poll.ContentType),
                Status = ToEnumString(poll.Status),
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                TotalVoteCount = counts.TotalVotes,
                MinVotes = counts.MinVotes,
                VoteProgress = counts.OtherVotes,
                ResultsUnlocked = counts.ResultsUnlocked,
                CustomAnswerCount = counts.CustomAnswerCount,
                TotalWhyCount = counts.WhyCount,
                PollOptions = pollOptions,
                YouVsCrowd = !counts.ResultsUnlocked ? new YouVsCrowdResponse() : new YouVsCrowdResponse
                {
                    YourOptionId = yourOptionId,
                    YourOptionText = yourOptionResponse?.Text ?? creatorVote?.CustomAnswer,
                    YourOptionPercentage = yourOptionResponse?.Percentage,
                    CrowdLeadingOptionId = crowdLeader?.Id,
                    CrowdLeadingOptionText = crowdLeader?.Text,
                    CrowdLeadingPercentage = crowdLeader?.Percentage,
                    AgreesWithCrowd = yourOptionId is null || crowdLeader is null
                        ? null
                        : yourOptionId == crowdLeader.Id
                }
            };
        }

        private static PollMediaResponse ToMediaResponse(PollMedia media)
        {
            return new PollMediaResponse
            {
                MediaType = ToEnumString(media.MediaType),
                MimeType = media.MimeType,
                FileSize = media.FileSize,
                DurationMs = media.DurationMs
            };
        }

        private static PollOptionResponse ToOptionResponse(PollOption option, PollVoteCountSnapshot counts)
        {
            var voteCount = counts.CountFor(option.Id);
            return new PollOptionResponse
            {
                Id = option.Id,
                Text = option.Text,
                SortOrder = option.SortOrder,
                VoteCount = voteCount,
                WhyCount = counts.WhyCountFor(option.Id),
                Percentage = ToPercentage(voteCount, counts.TotalVotes)
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

        private static string ToEnumString<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            return value.ToString().ToLowerInvariant();
        }
    }
}
