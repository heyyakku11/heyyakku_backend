using Yakku.Application.Polls;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Sharing.DTOs;

namespace Yakku.Application.Sharing
{
    /// <summary>Builds the one response shape for a poll or a made check and applies the results visibility rule.</summary>
    public static class SharedContentMapper
    {
        public static SharedContentResponse ToResponse(
            VotableContent content,
            PollVoteCountSnapshot tally,
            MyVote? myVote,
            VoterIdentity? viewer,
            DateTime utcNow)
        {
            var isOwner = viewer is { IsUser: true } && viewer.Id == content.OwnerId;
            var hasVoted = myVote is not null;

            // Counts and percentages are only for the owner or someone who voted, and only once enough other people did.
            var resultsVisible = tally.ResultsUnlocked && (isOwner || hasVoted);

            var response = new SharedContentResponse
            {
                ContentType = content.IsPoll ? ContentTypes.Poll : ContentTypes.Check,
                ContentId = content.ContentId,
                ShareCode = content.ShareCode,
                Question = content.Question,
                Status = content.Status,
                ExpiresAt = content.ExpiresAt,
                CreatedAt = content.CreatedAt,
                IsOwner = isOwner,
                Voting = BuildVotingState(content, myVote, isOwner, utcNow),
                Stats = new VoteStatsResponse
                {
                    ResultsVisible = resultsVisible,
                    MinVotesToReveal = tally.MinVotes,
                    VoteProgress = tally.OtherVotes,
                    TotalVotes = resultsVisible ? tally.TotalVotes : null,
                    CustomAnswerCount = resultsVisible ? tally.CustomAnswerCount : null,
                    CustomAnswerPercentage = resultsVisible ? Percentage(tally.CustomAnswerCount, tally.TotalVotes) : null,
                    WhyCount = resultsVisible ? tally.WhyCount : null
                },
                Options = content.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new SharedOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        DisplayOrder = option.DisplayOrder,
                        VoteCount = resultsVisible ? tally.CountFor(option.Id) : null,
                        Percentage = resultsVisible ? Percentage(tally.CountFor(option.Id), tally.TotalVotes) : null,
                        WhyCount = resultsVisible ? tally.WhyCountFor(option.Id) : null,
                        IsSelected = myVote?.OptionId == option.Id
                    })
                    .ToList()
            };

            if (content.Poll is not null)
            {
                response.Poll = new PollDetailsResponse
                {
                    PollType = content.Poll.PollType,
                    Media = content.Poll.Media is null
                        ? null
                        : new PollMediaResponse
                        {
                            MediaType = content.Poll.Media.MediaType.ToString().ToUpperInvariant(),
                            MimeType = content.Poll.Media.MimeType,
                            FileSize = content.Poll.Media.FileSize,
                            DurationMs = content.Poll.Media.DurationMs
                        }
                };
            }

            if (content.Check is not null)
            {
                response.Check = new CheckDetailsResponse
                {
                    CheckId = content.Check.CheckId,
                    Title = content.Check.Title,
                    BackgroundColor = content.Check.BackgroundColor,
                    Sticker = content.Check.Sticker
                };
            }

            return response;
        }

        /// <summary>Share of <paramref name="total"/> in percent, rounded to two decimals; 0 when nobody voted.</summary>
        public static decimal Percentage(int count, int total)
        {
            return total <= 0 ? 0m : Math.Round(count * 100m / total, 2);
        }

        private static VotingStateResponse BuildVotingState(
            VotableContent content,
            MyVote? myVote,
            bool isOwner,
            DateTime utcNow)
        {
            string? blocked = null;
            if (myVote is not null)
            {
                blocked = "ALREADY_VOTED";
            }
            else if (isOwner && !content.AllowOwnerVote)
            {
                blocked = "OWNER";
            }
            else
            {
                blocked = content.ClosedReason(utcNow);
            }

            return new VotingStateResponse
            {
                CanVote = blocked is null,
                HasVoted = myVote is not null,
                BlockedReason = blocked,
                MyVote = myVote is null
                    ? null
                    : new MyVoteResponse
                    {
                        OptionId = myVote.OptionId,
                        CustomAnswer = myVote.CustomAnswer,
                        Reason = myVote.Reason,
                        VotedAt = myVote.VotedAt
                    }
            };
        }
    }
}
