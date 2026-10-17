using System.Security.Cryptography;
using FluentValidation;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Votes;
using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Services
{
    public class CheckService : ICheckService
    {
        private const string ShareCodeAlphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int ShareCodeLength = 10;
        private const int ShareCodeAttempts = 5;

        private readonly ICheckRepository _repository;
        private readonly IValidator<CastCheckVoteRequest> _voteValidator;

        public CheckService(
            ICheckRepository repository,
            IValidator<CastCheckVoteRequest> voteValidator)
        {
            _repository = repository;
            _voteValidator = voteValidator;
        }

        public async Task<List<CheckResponse>> GetChecksAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var currentWeek = CheckWeek.Current();
            var checks = await _repository.GetLiveAsync(currentWeek, cancellationToken);
            var used = await _repository.GetUserChecksByCheckAsync(userId, cancellationToken);

            return checks
                .Select(check => ToCheckResponse(check, currentWeek, used.GetValueOrDefault(check.Id)))
                .ToList();
        }

        public async Task<List<CheckResponse>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var all = await GetChecksAsync(userId, cancellationToken);
            return all.Where(check => check.IsCurrent).ToList();
        }

        public async Task<UserCheckResponse> MakeAsync(
            Guid checkId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Only checks that are already live can be made; upcoming ones stay hidden until their week.
            var check = await _repository.GetLiveByIdAsync(checkId, CheckWeek.Current(), cancellationToken)
                ?? throw CheckExceptions.NotFound();

            var used = await _repository.GetUserChecksByCheckAsync(userId, cancellationToken);
            if (used.ContainsKey(check.Id))
            {
                throw CheckExceptions.AlreadyUsed();
            }

            for (var attempt = 0; attempt < ShareCodeAttempts; attempt++)
            {
                var userCheck = UserCheck.Create(userId, check.Id, NewShareCode());
                if (await _repository.TryAddUserCheckAsync(userCheck, cancellationToken))
                {
                    var saved = await _repository.GetUserCheckByIdAsync(userCheck.Id, cancellationToken)
                        ?? throw CheckExceptions.NotFound();
                    return ToUserCheckResponse(saved, userId);
                }
            }

            throw new AppException(
                500,
                ApiErrorCodes.InternalServerError,
                "Could not create a share code. Please try again.");
        }

        public async Task<List<UserCheckResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var items = await _repository.GetUserChecksAsync(userId, cancellationToken);
            return items.Select(item => ToUserCheckResponse(item, userId)).ToList();
        }

        public async Task<UserCheckResponse> GetMineByIdAsync(
            Guid userCheckId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetUserCheckByIdAsync(userCheckId, cancellationToken);
            if (item is null || item.UserId != userId)
            {
                throw CheckExceptions.NotFound();
            }

            return ToUserCheckResponse(item, userId);
        }

        public async Task<UserCheckResponse> GetSharedAsync(
            string shareCode,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetUserCheckByShareCodeAsync(shareCode, cancellationToken)
                ?? throw CheckExceptions.NotFound();
            return ToUserCheckResponse(item, userId);
        }

        public async Task<CheckVoteResponse> VoteAsync(
            string shareCode,
            Guid userId,
            CastCheckVoteRequest request,
            CancellationToken cancellationToken = default)
        {
            await _voteValidator.ValidateAndThrowAsync(request, cancellationToken);

            var item = await _repository.GetUserCheckByShareCodeAsync(shareCode, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            if (item.UserId == userId)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.Forbidden,
                    "You cannot answer your own check.");
            }

            if (item.Votes.Any(vote => vote.VoterId == userId))
            {
                throw VoteExceptions.AlreadyVoted();
            }

            var option = item.Check.Options.FirstOrDefault(candidate => candidate.Id == request.CheckOptionId);
            if (option is null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Option does not belong to this check.",
                    "checkOptionId");
            }

            var vote = UserCheckVote.Create(
                item.Id,
                item.CheckId,
                option.Id,
                userId,
                string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());
            await _repository.AddVoteAsync(vote, cancellationToken);

            return new CheckVoteResponse
            {
                Id = vote.Id,
                UserCheckId = vote.UserCheckId,
                CheckOptionId = vote.CheckOptionId,
                Reason = vote.Reason,
                CreatedAt = vote.CreatedAt
            };
        }

        private static CheckResponse ToCheckResponse(Check check, DateOnly currentWeek, UserCheck? used)
        {
            return new CheckResponse
            {
                Id = check.Id,
                WeekStart = check.LiveWeekStart,
                SequenceNo = check.SequenceNo,
                Title = check.Title,
                Question = check.Question,
                BackgroundColor = check.BackgroundColor,
                Sticker = check.Sticker,
                Options = check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                    .ToList(),
                IsCurrent = check.LiveWeekStart == currentWeek,
                Used = used is not null,
                UserCheckId = used?.Id,
                ShareCode = used?.ShareCode
            };
        }

        private static UserCheckResponse ToUserCheckResponse(UserCheck item, Guid viewerId)
        {
            var isOwner = item.UserId == viewerId;
            var myVote = item.Votes.FirstOrDefault(vote => vote.VoterId == viewerId);

            // Results are visible to the owner, or to a voter once they have answered.
            var showResults = isOwner || myVote is not null;
            var counts = item.Votes.GroupBy(vote => vote.CheckOptionId).ToDictionary(g => g.Key, g => g.Count());

            return new UserCheckResponse
            {
                Id = item.Id,
                CheckId = item.CheckId,
                Title = item.Check.Title,
                Question = item.Check.Question,
                BackgroundColor = item.Check.BackgroundColor,
                Sticker = item.Check.Sticker,
                ShareCode = item.ShareCode,
                IsOwner = isOwner,
                MyVoteOptionId = myVote?.CheckOptionId,
                TotalVotes = showResults ? item.Votes.Count : null,
                Options = item.Check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new UserCheckOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        Votes = showResults ? counts.GetValueOrDefault(option.Id) : null
                    })
                    .ToList(),
                CreatedAt = item.CreatedAt
            };
        }

        private static string NewShareCode()
        {
            return RandomNumberGenerator.GetString(ShareCodeAlphabet, ShareCodeLength);
        }
    }
}
