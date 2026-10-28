using Yakku.Application.Common;
using Yakku.Application.Users.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Users.Mapper
{
    internal static class UserAskMappings
    {
        /// <summary>
        /// A check the user made, in the same shape as a poll card so "My asks" is one list. Like a poll, the results
        /// stay hidden (only the progress is shown) until enough other people have answered.
        /// </summary>
        public static UserPollResponse ToUserAskResponse(this UserCheck item)
        {
            // The maker's own answer does not count towards the reveal limit.
            var otherVotes = item.Votes.Count(vote => vote.VoterId != item.UserId);
            var minVotes = OperationalSettings.Results.MinVotesToReveal;
            var unlocked = otherVotes >= minVotes;
            var totalVotes = item.Votes.Count;
            var optionCounts = item.Votes
                .Where(vote => vote.CheckOptionId is not null)
                .GroupBy(vote => vote.CheckOptionId!.Value)
                .ToDictionary(group => group.Key, group => group.Count());

            return new UserPollResponse
            {
                Kind = "check",
                PollId = item.Id,
                ShareCode = item.ShareCode,
                Question = item.Check.Question,
                ContentType = "check",
                Status = item.IsClosed ? "closed" : "active",
                CreatedAt = item.CreatedAt,
                TotalVoteCount = unlocked ? totalVotes : otherVotes,
                MinVotes = minVotes,
                VoteProgress = otherVotes,
                ResultsUnlocked = unlocked,
                CustomAnswerCount = unlocked ? item.Votes.Count(vote => vote.CustomAnswer is not null) : 0,
                TotalWhyCount = unlocked ? item.Votes.Count(vote => vote.Reason is not null) : 0,
                PollOptions = item.Check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option =>
                    {
                        var count = unlocked ? optionCounts.GetValueOrDefault(option.Id) : 0;
                        return new UserPollOptionResponse
                        {
                            Id = option.Id,
                            Text = option.Text,
                            VoteCount = count,
                            Percentage = unlocked && totalVotes > 0
                                ? Math.Round(count * 100m / totalVotes, 2)
                                : 0m
                        };
                    })
                    .ToList(),
                Check = new UserCheckStyleResponse
                {
                    Title = item.Check.Title,
                    BackgroundColor = item.Check.BackgroundColor,
                    Sticker = item.Check.Sticker
                }
            };
        }
    }
}
