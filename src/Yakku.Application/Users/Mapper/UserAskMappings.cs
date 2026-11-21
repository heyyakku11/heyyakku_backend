using Yakku.Application.Common;
using Yakku.Application.Polls;
using Yakku.Application.Users.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Users.Mapper
{
    internal static class UserAskMappings
    {
        /// <summary>
        /// A check the user made, as a list summary (no per-option breakdown / full theme config).
        /// </summary>
        public static UserPollResponse ToUserAskResponse(this UserCheck item, PollVoteCountSnapshot tally)
        {
            var display = tally.ForDisplay();
            var theme = Checks.Themes.CheckThemeMapper.ToResponse(item.Check);

            return new UserPollResponse
            {
                Kind = "check",
                PollId = item.Id,
                ShareCode = item.ShareCode,
                Question = item.Check.Question,
                ContentType = "check",
                Status = item.IsClosed ? "closed" : "active",
                CreatedAt = item.CreatedAt,
                ClosedAt = item.ClosedAt,
                ExpiresAt = item.ExpiresAt,
                TotalVoteCount = display.TotalVotes,
                MinVotes = OperationalSettings.Results.MinVotesToReveal,
                VoteProgress = display.OtherVotes,
                ResultsUnlocked = display.ResultsUnlocked,
                CustomAnswerCount = display.CustomAnswerCount,
                TotalWhyCount = display.WhyCount,
                PollOptions = [],
                Check = new UserCheckStyleResponse
                {
                    CheckType = theme.CheckType,
                    Theme = null,
                    Title = item.Check.Title,
                    BackgroundColor = item.Check.CardTheme?.BackgroundColor ?? theme.Config.BackgroundColor
                }
            };
        }
    }
}
