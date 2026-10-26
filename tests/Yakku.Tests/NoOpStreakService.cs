using Yakku.Application.Streaks;
using Yakku.Application.Streaks.DTOs;
using Yakku.Application.Streaks.Interfaces;

namespace Yakku.Tests;

/// <summary>For unit tests that do not exercise streaks; records the credits it is asked for.</summary>
internal sealed class NoOpStreakService : IStreakService
{
    public List<(Guid UserId, StreakActivity Activity, DateTime At)> Recorded { get; } = [];

    public Task<StreakCreditResult> RecordAsync(
        Guid userId,
        StreakActivity activity,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        Recorded.Add((userId, activity, occurredAtUtc));
        return Task.FromResult(new StreakCreditResult(true, 1, 1, StreakCalendar.ToActivityDate(occurredAtUtc)));
    }

    public Task<StreakResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new StreakResponse());

    public Task<StreakHistoryResponse> GetHistoryAsync(
        Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
        => Task.FromResult(new StreakHistoryResponse());

    public Task RebuildAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
