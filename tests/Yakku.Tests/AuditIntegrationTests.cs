using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Yakku.Application.Audit;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Audit;
using Yakku.Infrastructure.Configuration;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public sealed class AuditIntegrationTests
{
    private const string Jwt =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private readonly PostgresFixture _fixture;

    public AuditIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Authentication_StoresHashedSubject_RequestContext_AndNoSecrets()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var writer = NewWriter(ip: "203.0.113.9", userAgent: "Mozilla/5.0\r\nInjected: header");
        var email = $"jane.{Guid.NewGuid():N}@example.com";

        await writer.WriteAuthenticationAsync(new AuthenticationAuditEntry(
            AuditActorType.User,
            null,
            AuditEvents.Auth.OtpVerifyFailed,
            AuditOutcome.Suspicious,
            FailureReason: $"bad token {Jwt}",
            Subject: email,
            Metadata: new Dictionary<string, string?>
            {
                ["refreshToken"] = "super-secret-refresh",
                ["reason"] = "attempts"
            }));

        await using var context = _fixture.CreateContext();
        var hash = AuditSanitizer.HashSubject(email)!;
        var row = await context.AuthenticationAuditLogs.SingleAsync(log => log.SubjectHash == hash);
        Assert.Equal(AuditOutcome.Suspicious, row.Outcome);
        Assert.Equal(IPAddress.Parse("203.0.113.9"), row.IpAddress);
        Assert.DoesNotContain('\n', row.UserAgent!);
        Assert.DoesNotContain(Jwt, row.FailureReason);
        Assert.DoesNotContain("super-secret-refresh", row.Metadata ?? string.Empty);
        Assert.Contains("attempts", row.Metadata);
        Assert.DoesNotContain("jane", row.SubjectHash);
    }

    [SkippableFact]
    public async Task PollVote_StoresOnlyAPseudonym_NeverTheVoterOrTheirAnswer()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var writer = NewWriter();
        var pollId = Guid.NewGuid();
        var voter = Guid.NewGuid();

        await writer.WritePollAsync(new PollAuditEntry(
            pollId,
            AuditActorType.User,
            ActorUserId: voter,
            EventType: AuditEvents.Poll.VoteCast,
            Outcome: AuditOutcome.Success,
            VoterUserId: voter));

        await using var context = _fixture.CreateContext();
        var row = await context.PollAuditLogs.SingleAsync(log => log.PollId == pollId);
        Assert.Null(row.ActorUserId);
        Assert.Equal(AuditSanitizer.PseudonymizeVoter(voter, pollId), row.VoterRef);
        Assert.DoesNotContain(voter.ToString("N"), row.VoterRef);
        Assert.Null(row.Metadata);
    }

    [SkippableFact]
    public async Task PollHistory_SurvivesDeletionOfThePoll()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var writer = NewWriter();
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var poll = new Poll(user.Id, "Keep the history?", PollContentType.Question);
        poll.AddTextOption("Yes", 1);
        poll.AddTextOption("No", 2);
        context.Polls.Add(poll);
        await context.SaveChangesAsync();
        await writer.WritePollAsync(new PollAuditEntry(
            poll.Id,
            AuditActorType.User,
            user.Id,
            AuditEvents.Poll.Created,
            AuditOutcome.Success,
            ToStatus: "active"));

        context.Polls.Remove(poll);
        await context.SaveChangesAsync();

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Polls.AnyAsync(p => p.Id == poll.Id));
        Assert.True(await verify.PollAuditLogs.AnyAsync(log => log.PollId == poll.Id));
    }

    [SkippableFact]
    public async Task Notification_DistinguishesProviderAcceptanceFromDelivery()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var writer = NewWriter();
        var messageId = "msg-" + Guid.NewGuid().ToString("N")[..12];
        var correlation = Guid.NewGuid();

        await writer.WriteNotificationAsync(new NotificationAuditEntry(
            NotificationChannel.Email,
            "resend",
            AuditEvents.Notification.OtpEmail,
            null,
            null,
            "jane@example.com",
            correlation,
            AttemptNumber: 2,
            WillRetry: false,
            Status: NotificationAuditStatus.Accepted,
            ProviderMessageId: messageId,
            LatencyMs: 184));

        await using (var context = _fixture.CreateContext())
        {
            var accepted = await context.NotificationAuditLogs.SingleAsync(log => log.ProviderMessageId == messageId);
            Assert.Equal(NotificationAuditStatus.Accepted, accepted.Status);
            Assert.Equal(NotificationDeliveryStatus.Unknown, accepted.DeliveryStatus);
            Assert.NotNull(accepted.ProviderAcceptedAt);
            Assert.Null(accepted.DeliveryUpdatedAt);
            Assert.Equal("j***@example.com", accepted.Recipient);
            Assert.Equal(2, accepted.AttemptNumber);
            Assert.Equal(184, accepted.LatencyMs);
        }

        var applied = await writer.RecordNotificationDeliveryAsync(
            NotificationChannel.Email,
            "resend",
            messageId,
            NotificationDeliveryStatus.Delivered,
            DateTime.UtcNow);

        Assert.True(applied);
        await using var verify = _fixture.CreateContext();
        var delivered = await verify.NotificationAuditLogs.SingleAsync(log => log.ProviderMessageId == messageId);
        Assert.Equal(NotificationAuditStatus.Accepted, delivered.Status);
        Assert.Equal(NotificationDeliveryStatus.Delivered, delivered.DeliveryStatus);
        Assert.NotNull(delivered.DeliveryUpdatedAt);
    }

    [SkippableFact]
    public async Task Notification_FailedAttemptCannotBeReportedAsDelivered_AndErrorsAreSanitized()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var writer = NewWriter();
        var messageId = "failed-" + Guid.NewGuid().ToString("N")[..12];

        await writer.WriteNotificationAsync(new NotificationAuditEntry(
            NotificationChannel.Push,
            "fcm",
            AuditEvents.Notification.Push,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            1,
            false,
            NotificationAuditStatus.Failed,
            ProviderMessageId: messageId,
            ErrorCode: "invalid_token",
            ErrorMessage: $"Token {Jwt} rejected for jane@example.com"));

        Assert.False(await writer.RecordNotificationDeliveryAsync(
            NotificationChannel.Push,
            "fcm",
            messageId,
            NotificationDeliveryStatus.Delivered,
            DateTime.UtcNow));
        Assert.False(await writer.RecordNotificationDeliveryAsync(
            NotificationChannel.Push,
            "fcm",
            "no-such-message",
            NotificationDeliveryStatus.Delivered,
            DateTime.UtcNow));

        await using var context = _fixture.CreateContext();
        var row = await context.NotificationAuditLogs.SingleAsync(log => log.ProviderMessageId == messageId);
        Assert.Equal(NotificationDeliveryStatus.NotApplicable, row.DeliveryStatus);
        Assert.DoesNotContain(Jwt, row.ErrorMessage);
        Assert.DoesNotContain("jane@example.com", row.ErrorMessage);
    }

    [SkippableFact]
    public async Task SystemLog_SanitizesMessagePathAndStatus()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var writer = NewWriter();
        var id = Guid.NewGuid();
        var marker = "marker-" + id.ToString("N")[..8];

        await writer.WriteSystemAsync(new SystemLogEntry(
            SystemLogCategory.Exception,
            SystemLogSeverity.Error,
            marker,
            $"Failed for jane@example.com with Bearer {Jwt}",
            ExceptionType: "System.InvalidOperationException",
            HttpMethod: "POST",
            Path: $"/api/v1/polls/{id}/votes?token=secret",
            StatusCode: 999,
            DurationMs: 12));

        await using var context = _fixture.CreateContext();
        var row = await context.SystemLogs.SingleAsync(log => log.Source == marker);
        Assert.DoesNotContain("jane@example.com", row.Message);
        Assert.DoesNotContain(Jwt, row.Message);
        Assert.Equal("/api/v1/polls/{id}/votes", row.Path);
        Assert.Null(row.StatusCode);
        Assert.Equal(12, row.DurationMs);
    }

    [SkippableFact]
    public async Task Writer_NeverThrows_WhenTheDatabaseIsUnreachable()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var services = new ServiceCollection();
        services.AddDbContext<YakkuDbContext>(options => options.UseNpgsql(
            "Host=127.0.0.1;Port=1;Database=none;Username=x;Password=x;Timeout=1;Command Timeout=1"));
        await using var provider = services.BuildServiceProvider();
        var writer = new AuditLogWriter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new HttpContextAccessor(),
            NullLogger<AuditLogWriter>.Instance);

        await writer.WriteAuthenticationAsync(new AuthenticationAuditEntry(
            AuditActorType.User, null, AuditEvents.Auth.LoggedIn, AuditOutcome.Success));
        await writer.WritePollAsync(new PollAuditEntry(
            Guid.NewGuid(), AuditActorType.User, Guid.NewGuid(), AuditEvents.Poll.Created, AuditOutcome.Success));
        await writer.WriteSystemAsync(new SystemLogEntry(
            SystemLogCategory.Exception, SystemLogSeverity.Error, "test", "boom"));
        Assert.False(await writer.RecordNotificationDeliveryAsync(
            NotificationChannel.Email, "resend", "x", NotificationDeliveryStatus.Delivered, DateTime.UtcNow));
    }

    [SkippableFact]
    public async Task Constraints_RejectInvalidAuditRows()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var now = DateTime.UtcNow;

        // Voter references are only valid on vote events, and never next to a user id.
        var voterOnWrongEvent = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO poll_audit_logs (id, poll_id, actor_type, voter_ref, event_type, outcome, created_at)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, 'USER', 'abc', 'poll.created', 'SUCCESS', {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, voterOnWrongEvent.SqlState);

        var voterWithUser = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO poll_audit_logs (id, poll_id, actor_type, actor_user_id, voter_ref, event_type, outcome, created_at)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, 'USER', {Guid.NewGuid()}, 'abc', 'vote.cast', 'SUCCESS', {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, voterWithUser.SqlState);

        // A delivery outcome requires the provider to have accepted the message.
        var deliveredButFailed = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification_audit_logs
                (id, channel, provider, attempt_number, will_retry, status, delivery_status, created_at)
            VALUES ({Guid.NewGuid()}, 'EMAIL', 'resend', 1, false, 'FAILED', 'DELIVERED', {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, deliveredButFailed.SqlState);

        var badAttempt = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification_audit_logs
                (id, channel, provider, attempt_number, will_retry, status, delivery_status, created_at)
            VALUES ({Guid.NewGuid()}, 'EMAIL', 'resend', 0, false, 'FAILED', 'NOTAPPLICABLE', {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badAttempt.SqlState);

        var badChannel = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification_audit_logs
                (id, channel, provider, attempt_number, will_retry, status, delivery_status, created_at)
            VALUES ({Guid.NewGuid()}, 'PIGEON', 'coo', 1, false, 'FAILED', 'NOTAPPLICABLE', {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badChannel.SqlState);

        var badOutcome = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO authentication_audit_logs (id, actor_type, event_type, outcome, created_at)
            VALUES ({Guid.NewGuid()}, 'USER', 'login.succeeded', 'MAYBE', {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badOutcome.SqlState);

        var badStatusCode = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO system_logs (id, category, severity, source, message, status_code, created_at)
            VALUES ({Guid.NewGuid()}, 'EXCEPTION', 'ERROR', 'x', 'y', 42, {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, badStatusCode.SqlState);
    }

    [SkippableFact]
    public async Task Retention_RemovesOnlyRowsPastTheirWindow_AndKeepsPollHistoryLongest()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var now = DateTime.UtcNow;
        var tag = "retention-" + Guid.NewGuid().ToString("N")[..8];

        // Same age (200 days), different windows: auth 180d => gone, poll 730d => kept.
        var oldAuth = new AuthenticationAuditLog(
            AuditActorType.User, null, tag, "login.succeeded", AuditOutcome.Success, null, null, null, null, null, null, now.AddDays(-200));
        var freshAuth = new AuthenticationAuditLog(
            AuditActorType.User, null, tag, "login.succeeded", AuditOutcome.Success, null, null, null, null, null, null, now.AddDays(-10));
        var pollRow = new PollAuditLog(
            Guid.NewGuid(), AuditActorType.User, Guid.NewGuid(), null, "poll.created", AuditOutcome.Success, null, "active", null, null, null, now.AddDays(-200));
        var ancientPollRow = new PollAuditLog(
            Guid.NewGuid(), AuditActorType.User, Guid.NewGuid(), null, "poll.created", AuditOutcome.Success, null, "active", null, null, null, now.AddDays(-800));
        var oldSystem = new SystemLog(
            SystemLogCategory.Latency, SystemLogSeverity.Warning, tag, "Slow request.", null, null, null, null, null, null, null, now.AddDays(-45));
        var oldNotification = new NotificationAuditLog(
            NotificationChannel.Email, "resend", tag, null, null, null, null, 1, false, NotificationAuditStatus.Accepted, null, null, null, null, now.AddDays(-100));
        context.AddRange(oldAuth, freshAuth, pollRow, ancientPollRow, oldSystem, oldNotification);
        await context.SaveChangesAsync();

        var result = await new AuditRetentionService(context, AuditRetentionOptions.Default, new RecordingAuditLogWriter())
            .PurgeAsync(now);

        Assert.True(result.AuthenticationDeleted >= 1);
        Assert.True(result.SystemDeleted >= 1);
        Assert.True(result.NotificationDeleted >= 1);
        Assert.True(result.PollDeleted >= 1);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.AuthenticationAuditLogs.AnyAsync(log => log.Id == oldAuth.Id));
        Assert.True(await verify.AuthenticationAuditLogs.AnyAsync(log => log.Id == freshAuth.Id));
        Assert.True(await verify.PollAuditLogs.AnyAsync(log => log.Id == pollRow.Id));
        Assert.False(await verify.PollAuditLogs.AnyAsync(log => log.Id == ancientPollRow.Id));
        Assert.False(await verify.SystemLogs.AnyAsync(log => log.Id == oldSystem.Id));
        Assert.False(await verify.NotificationAuditLogs.AnyAsync(log => log.Id == oldNotification.Id));
    }

    [SkippableFact]
    public async Task Retention_RecordsItsOwnRunInSystemLogs()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        await new AuditRetentionService(context, AuditRetentionOptions.Default, NewWriter())
            .PurgeAsync(DateTime.UtcNow);

        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.SystemLogs.AnyAsync(log =>
            log.Source == nameof(AuditRetentionService) && log.Category == SystemLogCategory.BackgroundJob));
    }

    private AuditLogWriter NewWriter(string? ip = null, string? userAgent = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<YakkuDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
        var provider = services.BuildServiceProvider();

        var accessor = new HttpContextAccessor();
        if (ip is not null || userAgent is not null)
        {
            var http = new DefaultHttpContext();
            if (ip is not null)
            {
                http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            }

            if (userAgent is not null)
            {
                http.Request.Headers.UserAgent = userAgent;
            }

            accessor.HttpContext = http;
        }

        return new AuditLogWriter(
            provider.GetRequiredService<IServiceScopeFactory>(),
            accessor,
            NullLogger<AuditLogWriter>.Instance);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var phone = "+1" + string.Concat(suffix.Where(char.IsDigit).Concat("0123456789").Take(10));
        var user = new User(phone, "Audit Tester", "anon-" + suffix);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
