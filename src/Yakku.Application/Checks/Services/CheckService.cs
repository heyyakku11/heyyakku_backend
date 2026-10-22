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

        public async Task<List<CheckResponse>> GetChecksAsync(Guid? userId, CancellationToken cancellationToken = default)
        {
            var currentWeek = CheckWeek.Current();
            var checks = await _repository.GetForWeekAsync(currentWeek, cancellationToken);
            var used = userId is null
                ? new Dictionary<Guid, UserCheck>()
                : await _repository.GetUserChecksByCheckAsync(userId.Value, cancellationToken);

            return checks
                .Select(check => ToCheckResponse(check, used.GetValueOrDefault(check.Id)))
                .ToList();
        }

        public async Task<CheckResponse> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken = default)
        {
            var currentWeek = CheckWeek.Current();
            var check = await _repository.GetLiveByIdAsync(id, currentWeek, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            UserCheck? used = null;
            if (userId is not null)
            {
                var usedByCheck = await _repository.GetUserChecksByCheckAsync(userId.Value, cancellationToken);
                used = usedByCheck.GetValueOrDefault(check.Id);
            }

            return ToCheckResponse(check, used);
        }

        public async Task<UserCheckResponse> MakeAsync(
            Guid userId,
            MakeCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.CheckId == Guid.Empty)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "checkId is required.",
                    "checkId");
            }

            var answer = new CastCheckVoteRequest
            {
                CheckOptionId = request.CheckOptionId,
                CustomAnswer = request.CustomAnswer,
                Reason = request.Reason
            };
            await _voteValidator.ValidateAndThrowAsync(answer, cancellationToken);

            // Only checks that are already live can be made; upcoming ones stay hidden until their week.
            var check = await _repository.GetLiveByIdAsync(request.CheckId, CheckWeek.Current(), cancellationToken)
                ?? throw CheckExceptions.NotFound();

            var used = await _repository.GetUserChecksByCheckAsync(userId, cancellationToken);
            if (used.ContainsKey(check.Id))
            {
                throw CheckExceptions.AlreadyUsed();
            }

            for (var attempt = 0; attempt < ShareCodeAttempts; attempt++)
            {
                var userCheck = UserCheck.Create(userId, check.Id, ShareCodeGenerator.New(userId, DateTime.UtcNow));

                // The maker's own answer is saved together with the check, so a check is never used without one.
                var ownerAnswer = BuildVote(userCheck.Id, check, userId, answer);
                if (await _repository.TryAddUserCheckAsync(userCheck, ownerAnswer, cancellationToken))
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
            Guid? userId,
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

            var vote = BuildVote(item.Id, item.Check, userId, request);
            await _repository.AddVoteAsync(vote, cancellationToken);

            return new CheckVoteResponse
            {
                Id = vote.Id,
                UserCheckId = vote.UserCheckId,
                CheckOptionId = vote.CheckOptionId,
                CustomAnswer = vote.CustomAnswer,
                Reason = vote.Reason,
                CreatedAt = vote.CreatedAt
            };
        }

        /// <summary>Builds the vote from an already validated request (exactly one of option or custom answer).</summary>
        private static UserCheckVote BuildVote(Guid userCheckId, Check check, Guid voterId, CastCheckVoteRequest request)
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            if (request.CheckOptionId is not null)
            {
                var option = check.Options.FirstOrDefault(candidate => candidate.Id == request.CheckOptionId.Value);
                if (option is null)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Option does not belong to this check.",
                        "checkOptionId");
                }

                return UserCheckVote.ForOption(userCheckId, check.Id, option.Id, voterId, reason);
            }

            return UserCheckVote.ForCustomAnswer(userCheckId, check.Id, request.CustomAnswer!.Trim(), voterId, reason);
        }

        private static CheckResponse ToCheckResponse(Check check, UserCheck? used)
        {
            return new CheckResponse
            {
                Id = check.Id,
                WeekStart = check.LiveWeekStart,
                Title = check.Title,
                Question = check.Question,
                BackgroundColor = check.BackgroundColor,
                Sticker = check.Sticker,
                Options = check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                    .ToList(),
                Used = used is not null,
            };
        }

        private static UserCheckResponse ToUserCheckResponse(UserCheck item, Guid? viewerId)
        {
            var isOwner = item.UserId == viewerId;
            var myVote = item.Votes.FirstOrDefault(vote => vote.VoterId == viewerId);

            // Results are visible to the owner, or to a voter once they have answered.
            var showResults = isOwner || myVote is not null;
            var counts = item.Votes
                .Where(vote => vote.CheckOptionId is not null)
                .GroupBy(vote => vote.CheckOptionId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

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
                MyCustomAnswer = myVote?.CustomAnswer,
                TotalVotes = showResults ? item.Votes.Count : null,
                CustomAnswerVotes = showResults ? item.Votes.Count(vote => vote.CustomAnswer is not null) : null,
                Options = item.Check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new UserCheckOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        Votes = showResults ? counts.GetValueOrDefault(option.Id) : null,
                        WhyCount = showResults
                            ? item.Votes.Count(vote => vote.CheckOptionId == option.Id && vote.Reason != null)
                            : null
                    })
                    .ToList(),
                CreatedAt = item.CreatedAt
            };
        }

    }
}
