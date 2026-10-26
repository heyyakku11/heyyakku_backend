using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Streaks.DTOs;
using Yakku.Application.Streaks.Interfaces;

namespace Yakku.Application.Streaks.Services
{
    public class StreakService : IStreakService
    {
        public const int MaxHistoryDays = 366;
        private const int DefaultHistoryDays = 30;

        private readonly IStreakRepository _streaks;
        private readonly TimeProvider _time;
        private readonly ILogger<StreakService> _logger;

        public StreakService(IStreakRepository streaks, TimeProvider time, ILogger<StreakService> logger)
        {
            _streaks = streaks;
            _time = time;
            _logger = logger;
        }

        public async Task<StreakCreditResult> RecordAsync(
            Guid userId,
            StreakActivity activity,
            DateTime occurredAtUtc,
            CancellationToken cancellationToken = default)
        {
            var date = StreakCalendar.ToActivityDate(occurredAtUtc);
            if (userId == Guid.Empty || !StreakRules.Qualifies(activity))
            {
                return new StreakCreditResult(false, 0, 0, date);
            }

            var firstActivityAt = DateTime.SpecifyKind(
                occurredAtUtc.Kind == DateTimeKind.Local ? occurredAtUtc.ToUniversalTime() : occurredAtUtc,
                DateTimeKind.Utc);
            var now = _time.GetUtcNow().UtcDateTime;
            StreakCreditResult result = new(false, 0, 0, date);

            await _streaks.ExecuteInTransactionAsync(async transactionToken =>
            {
                // 1. The unique (user, day) row is the idempotency key. Losing it means the day is already counted.
                if (!await _streaks.TryAddDayAsync(userId, date, firstActivityAt, transactionToken))
                {
                    var existing = await _streaks.GetSummaryAsync(userId, transactionToken);
                    result = new StreakCreditResult(false, existing?.Current ?? 0, existing?.Longest ?? 0, date);
                    return;
                }

                // 2. A new day: lock the summary row so concurrent writers of this user apply one at a time.
                var current = await _streaks.LockSummaryAsync(userId, transactionToken);
                if (current is null)
                {
                    var first = StreakMath.Next(null, date).State;
                    if (await _streaks.TryCreateSummaryAsync(userId, first, now, transactionToken))
                    {
                        result = new StreakCreditResult(true, first.Current, first.Longest, date);
                        return;
                    }

                    // Another request created it between our read and insert; continue under its row lock.
                    current = await _streaks.LockSummaryAsync(userId, transactionToken)
                        ?? throw new InvalidOperationException("The streak summary disappeared while it was locked.");
                }

                var step = StreakMath.Next(current, date);
                if (step.Transition == StreakTransition.Historical)
                {
                    // A day before the last counted one: history is authoritative, so repair from it rather than
                    // applying forward-only arithmetic to an old event.
                    _logger.LogInformation("Rebuilding a streak summary after an out-of-order activity day.");
                    var rebuilt = await _streaks.RebuildSummaryAsync(userId, now, transactionToken);
                    result = new StreakCreditResult(
                        true, rebuilt?.Current ?? 0, rebuilt?.Longest ?? 0, date);
                    return;
                }

                if (step.State != current)
                {
                    await _streaks.UpdateSummaryAsync(userId, step.State, now, transactionToken);
                }

                result = new StreakCreditResult(true, step.State.Current, step.State.Longest, date);
            }, cancellationToken);

            return result;
        }

        public async Task<StreakResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var today = Today();
            var state = await _streaks.GetSummaryAsync(userId, cancellationToken);
            return new StreakResponse
            {
                CurrentStreak = StreakMath.EffectiveCurrent(state, today),
                LongestStreak = state?.Longest ?? 0,
                LastActivityDate = state?.LastActivityDate,
                IsActiveToday = state is { } s && s.LastActivityDate >= today,
                Today = today
            };
        }

        public async Task<StreakHistoryResponse> GetHistoryAsync(
            Guid userId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default)
        {
            var end = to ?? Today();
            var start = from ?? end.AddDays(-(DefaultHistoryDays - 1));
            if (start > end)
            {
                throw new AppException(
                    400, ApiErrorCodes.ValidationError, "The start date must not be after the end date.", "from");
            }

            if (end.DayNumber - start.DayNumber + 1 > MaxHistoryDays)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    $"The range can cover at most {MaxHistoryDays} days.",
                    "from");
            }

            var days = await _streaks.GetDaysAsync(userId, start, end, cancellationToken);
            return new StreakHistoryResponse { From = start, To = end, ActiveDates = days.ToList() };
        }

        public async Task RebuildAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            await _streaks.ExecuteInTransactionAsync(
                async transactionToken =>
                {
                    await _streaks.LockSummaryAsync(userId, transactionToken);
                    await _streaks.RebuildSummaryAsync(userId, now, transactionToken);
                },
                cancellationToken);
        }

        private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
    }
}
