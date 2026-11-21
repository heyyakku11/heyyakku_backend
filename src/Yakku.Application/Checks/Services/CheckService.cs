using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Options;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Caching;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Polls;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.Streaks;
using Yakku.Application.Streaks.Interfaces;
using Yakku.Application.Users;
using Yakku.Application.Votes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Checks.Services
{
    public class CheckService : ICheckService
    {
        private const int ShareCodeAttempts = 5;

        private readonly ICheckRepository _repository;
        private readonly IValidator<CastCheckVoteRequest> _voteValidator;
        private readonly IStreakService _streaks;
        private readonly ICardLifecycleNotifications _lifecycle;
        private readonly IUserBlockLookup? _blocks;
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;

        public CheckService(
            ICheckRepository repository,
            IValidator<CastCheckVoteRequest> voteValidator,
            IStreakService streaks,
            ICardLifecycleNotifications? lifecycle = null,
            IUserBlockLookup? blocks = null,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null)
        {
            _blocks = blocks;
            _repository = repository;
            _voteValidator = voteValidator;
            _streaks = streaks;
            _lifecycle = lifecycle ?? NullCardLifecycleNotifications.Instance;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
        }

        public async Task<List<CheckResponse>> GetChecksAsync(
            Guid? userId,
            CancellationToken cancellationToken = default)
        {
            var currentWeek = CheckWeek.Current();
            // Shared catalog (no per-user Used) so Redis always has one stable week key.
            var catalog = await _cache.GetOrSetAsync(
                ResponseCacheKeys.ChecksWeekCatalog(currentWeek),
                _cacheOptions.WeeklyChecks,
                async ct =>
                {
                    var checks = await _repository.GetForWeekAsync(currentWeek, ct);
                    return checks
                        .Select(check => ToCheckResponse(check, used: null))
                        .ToList();
                },
                cancellationToken);

            if (userId is null || catalog.Count == 0)
            {
                return catalog;
            }

            var usedIds = await _cache.GetOrSetAsync(
                ResponseCacheKeys.ChecksUsed(userId.Value),
                _cacheOptions.UserCatalog,
                TimeSpan.FromSeconds(Math.Max(_cacheOptions.UserCatalogSeconds * 3, 180)),
                async ct =>
                {
                    var used = await _repository.GetUserChecksByCheckAsync(userId.Value, ct);
                    return used.Keys.ToList();
                },
                cancellationToken);
            var usedSet = usedIds.ToHashSet();
            foreach (var item in catalog)
            {
                item.Used = usedSet.Contains(item.Id);
            }

            return catalog;
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
            bool requireFun = false,
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
            if (check.IsFun != requireFun)
            {
                throw CheckExceptions.NotFound();
            }

            var used = await _repository.GetUserChecksByCheckAsync(userId, cancellationToken);
            if (used.ContainsKey(check.Id))
            {
                throw CheckExceptions.AlreadyUsed();
            }

            for (var attempt = 0; attempt < ShareCodeAttempts; attempt++)
            {
                // A card made from a fun card stays open for 24 hours; a made weekly check has no expiry.
                var expiresAt = check.IsFun ? DateTime.UtcNow + UserCheck.FunCardLifetime : (DateTime?)null;
                var userCheck = UserCheck.Create(userId, check.Id, ShareCodeGenerator.New(), expiresAt);

                // The maker's own answer is saved together with the check, so a check is never used without one.
                var ownerAnswer = BuildVote(userCheck.Id, check, userId, answer);
                // Making a check earns the streak day in the same transaction that stores it.
                if (await _repository.TryAddUserCheckAsync(
                        userCheck,
                        ownerAnswer,
                        token => _streaks.RecordAsync(
                            userId, StreakActivity.CheckMade, userCheck.CreatedAt, token),
                        cancellationToken))
                {
                    await _cache.RemoveManyAsync(
                        ResponseCacheKeys.AfterUserMadeCard(
                            userId,
                            CheckWeek.Current(),
                            check.IsFun ? check.CardThemeId : null),
                        cancellationToken);

                    var saved = await _repository.GetUserCheckByIdAsync(userCheck.Id, cancellationToken)
                        ?? throw CheckExceptions.NotFound();
                    return await MapUserCheckAsync(saved, userId, cancellationToken);
                }
            }

            throw new AppException(
                500,
                ApiErrorCodes.InternalServerError,
                "Could not create a share code. Please try again.");
        }

        public Task<UserChecksPage> GetMineAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.ChecksMine(userId, cursor),
                _cacheOptions.ListSoft,
                _cacheOptions.ListHard,
                ct => LoadMineAsync(userId, cursor, ct),
                cancellationToken);
        }

        private async Task<UserChecksPage> LoadMineAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken)
        {
            var decoded = PollCursor.TryDecode(cursor);
            var take = PollCursor.PageSize + 1;
            var items = await _repository.GetUserChecksPageAsync(
                userId,
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                cancellationToken);

            var hasMore = items.Count > PollCursor.PageSize;
            var page = items.Take(PollCursor.PageSize).ToList();
            var tallies = await _repository.GetVoteCountsAsync(
                page.Select(item => item.Id).ToArray(),
                cancellationToken);

            var mapped = new List<UserCheckResponse>(page.Count);
            foreach (var item in page)
            {
                var tally = tallies.GetValueOrDefault(item.Id) ?? PollVoteCountSnapshot.Empty;
                mapped.Add(ToUserCheckResponse(item, userId, tally, myVote: null, summaryOnly: true));
            }

            string? nextCursor = null;
            if (hasMore && page.Count > 0)
            {
                var last = page[^1];
                nextCursor = PollCursor.Encode(last.CreatedAt, last.Id);
            }

            return new UserChecksPage
            {
                Items = mapped,
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
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

            return await MapUserCheckAsync(item, userId, cancellationToken);
        }

        public async Task<UserCheckResponse> GetSharedAsync(
            string shareCode,
            Guid? userId,
            CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetUserCheckByShareCodeAsync(shareCode, cancellationToken)
                ?? throw CheckExceptions.NotFound();
            return await MapUserCheckAsync(item, userId, cancellationToken);
        }

        public async Task<UserCheckResponse> CloseMineAsync(
            Guid userCheckId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetOwnedUserCheckForUpdateAsync(userCheckId, userId, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            if (!item.TryClose())
            {
                throw CheckExceptions.AlreadyClosed();
            }

            await _repository.SaveChecksAsync(cancellationToken);
            await _lifecycle.NotifyCheckClosedAsync(userCheckId, cancellationToken);
            await InvalidateShareAsync(item.ShareCode, userId, cancellationToken);
            var closed = await _repository.GetUserCheckByIdAsync(userCheckId, cancellationToken)
                ?? throw CheckExceptions.NotFound();
            return await MapUserCheckAsync(closed, userId, cancellationToken);
        }

        public async Task<UserCheckResponse> ReshareMineAsync(
            Guid userCheckId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetOwnedUserCheckForUpdateAsync(userCheckId, userId, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            if (!item.TryReshare(DateTime.UtcNow))
            {
                throw CheckExceptions.NotClosed();
            }

            await _repository.SaveChecksAsync(cancellationToken);
            await InvalidateShareAsync(item.ShareCode, userId, cancellationToken);
            var reopened = await _repository.GetUserCheckByIdAsync(userCheckId, cancellationToken)
                ?? throw CheckExceptions.NotFound();
            return await MapUserCheckAsync(reopened, userId, cancellationToken);
        }

        public async Task DeleteMineAsync(
            Guid userCheckId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetOwnedUserCheckForUpdateAsync(userCheckId, userId, cancellationToken)
                ?? throw CheckExceptions.NotFound();
            var shareCode = item.ShareCode;
            await _repository.DeleteUserCheckAsync(item, cancellationToken);
            await _cache.RemoveManyAsync(
                ResponseCacheKeys.AfterUserMadeCard(userId, CheckWeek.Current()),
                cancellationToken);
            await InvalidateShareAsync(shareCode, userId, cancellationToken);
        }

        private async Task InvalidateShareAsync(string shareCode, Guid ownerId, CancellationToken cancellationToken)
        {
            var keys = ResponseCacheKeys.AfterShareMutation(shareCode, ownerId)
                .Concat(ResponseCacheKeys.AfterAskListMutation(ownerId))
                .ToArray();
            await _cache.RemoveManyAsync(keys, cancellationToken);
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

            // A card whose owner blocked the voter reads exactly like a closed one.
            if (item.IsClosed
                || (item.UserId != userId
                    && _blocks is not null
                    && await _blocks.IsBlockedAsync(item.UserId, userId, cancellationToken)))
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "This check is not accepting votes.");
            }

            if (item.UserId == userId)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.Forbidden,
                    "You cannot answer your own check.");
            }

            if (await _repository.GetViewerVoteAsync(item.Id, userId, cancellationToken) is not null)
            {
                throw VoteExceptions.AlreadyVoted();
            }

            var vote = BuildVote(item.Id, item.Check, userId, request);
            await _repository.AddVoteAsync(vote, cancellationToken);

            var keys = new List<string>(ResponseCacheKeys.AfterShareMutation(item.ShareCode, item.UserId, userId))
            {
                ResponseCacheKeys.CheckCustomAnswers(item.Id, item.UserId)
            };
            if (request.CheckOptionId is { } optionId)
            {
                keys.Add(ResponseCacheKeys.CheckOptionReasons(item.Id, optionId, item.UserId));
            }

            await _cache.RemoveManyAsync(keys, cancellationToken);

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
            var theme = CheckThemeMapper.ToResponse(check);
            return new CheckResponse
            {
                CheckType = theme.CheckType,
                Theme = theme,
                Id = check.Id,
                WeekStart = check.LiveWeekStart ?? default,
                Title = check.Title,
                Question = check.Question,
                BackgroundColor = theme.Config.BackgroundColor,
                Options = check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                    .ToList(),
                Used = used is not null,
            };
        }

        private async Task<UserCheckResponse> MapUserCheckAsync(
            UserCheck item,
            Guid? viewerId,
            CancellationToken cancellationToken)
        {
            var tallies = await _repository.GetVoteCountsAsync([item.Id], cancellationToken);
            var tally = tallies.GetValueOrDefault(item.Id) ?? PollVoteCountSnapshot.Empty;
            UserCheckVote? myVote = null;
            if (viewerId is { } vid)
            {
                myVote = await _repository.GetViewerVoteAsync(item.Id, vid, cancellationToken);
            }

            return ToUserCheckResponse(item, viewerId, tally, myVote, summaryOnly: false);
        }

        private static UserCheckResponse ToUserCheckResponse(
            UserCheck item,
            Guid? viewerId,
            PollVoteCountSnapshot tally,
            UserCheckVote? myVote,
            bool summaryOnly)
        {
            var display = tally.ForDisplay();
            var isOwner = item.UserId == viewerId;
            var voteProgress = display.OtherVotes;
            var unlocked = display.ResultsUnlocked;
            var showResults = unlocked && (isOwner || myVote is not null);

            var theme = CheckThemeMapper.ToResponse(item.Check);
            var options = summaryOnly
                ? []
                : item.Check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new UserCheckOptionResponse
                    {
                        Id = option.Id,
                        Text = option.Text,
                        Votes = showResults ? display.CountFor(option.Id) : null,
                        WhyCount = showResults ? display.WhyCountFor(option.Id) : null
                    })
                    .ToList();

            return new UserCheckResponse
            {
                CheckType = theme.CheckType,
                Theme = summaryOnly
                    ? new CheckThemeResponse
                    {
                        CheckType = theme.CheckType,
                        CheckTypeId = theme.CheckTypeId,
                        IsLegacy = theme.IsLegacy
                    }
                    : theme,
                Id = item.Id,
                CheckId = item.CheckId,
                Title = item.Check.Title,
                Question = item.Check.Question,
                BackgroundColor = theme.Config.BackgroundColor,
                ShareCode = item.ShareCode,
                ExpiresAt = item.ExpiresAt,
                IsClosed = item.IsClosed,
                ClosedAt = item.ClosedAt,
                IsOwner = isOwner,
                MyVoteOptionId = summaryOnly ? null : myVote?.CheckOptionId,
                MyCustomAnswer = summaryOnly ? null : myVote?.CustomAnswer,
                TotalVotes = showResults ? display.TotalVotes : null,
                MinVotes = OperationalSettings.Results.MinVotesToReveal,
                VoteProgress = voteProgress,
                ResultsUnlocked = unlocked,
                CustomAnswerVotes = showResults ? display.CustomAnswerCount : null,
                Options = options,
                CreatedAt = item.CreatedAt
            };
        }

    }
}
