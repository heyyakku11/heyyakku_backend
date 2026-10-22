using FluentValidation;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Services;
using Yakku.Application.Audit;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class AccountDeletionTests
{
    private const string Phone = "+15550000000";

    [Fact]
    public void RequestDeletion_SchedulesThirtyDaysOut_AndCancelClearsIt()
    {
        var user = new User(Phone, "Sam", "Quiet Fox");
        var now = DateTime.UtcNow;

        user.RequestDeletion(now, TimeSpan.FromDays(OperationalSettings.AccountDeletion.GraceDays));

        Assert.True(user.IsPendingDeletion);
        Assert.Equal(now.AddDays(30), user.DeletionScheduledFor);
        Assert.True(user.CancelDeletion());
        Assert.False(user.IsPendingDeletion);
        Assert.Null(user.DeletionRequestedAt);
        Assert.False(user.CancelDeletion());
    }

    [Fact]
    public async Task Request_SchedulesDeletion_RevokesSessions_AndIsIdempotent()
    {
        var h = new Harness();

        var first = await h.Service.RequestDeletionAsync(h.User.Id);
        var second = await h.Service.RequestDeletionAsync(h.User.Id);

        Assert.True(h.User.IsPendingDeletion);
        Assert.Equal(first.ScheduledFor, second.ScheduledFor);
        Assert.InRange(first.ScheduledFor, DateTime.UtcNow.AddDays(29.9), DateTime.UtcNow.AddDays(30.1));
        Assert.Equal(2, h.Sessions.RevokeAllCalls);
        Assert.Single(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.AccountDeletionRequested);
    }

    [Fact]
    public async Task Request_ForUnknownUser_Returns404()
    {
        var h = new Harness();

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Service.RequestDeletionAsync(Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Login_WithinTheGracePeriod_CancelsTheDeletion()
    {
        var h = new Harness();
        await h.Service.RequestDeletionAsync(h.User.Id);
        h.Otp.Set(Phone, new OtpChallenge { OtpHash = "123456", Purpose = OtpPurpose.Login, CreatedAt = DateTime.UtcNow });

        var response = await h.Auth.VerifyOtpAsync(new VerifyOtpRequest { PhoneNumber = Phone, Otp = "123456" });

        Assert.True(response.AccountRestored);
        Assert.False(h.User.IsPendingDeletion);
        Assert.Contains(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.AccountDeletionCancelled);
    }

    [Fact]
    public async Task Login_OfAnActiveAccount_IsNotReportedAsRestored()
    {
        var h = new Harness();
        h.Otp.Set(Phone, new OtpChallenge { OtpHash = "123456", Purpose = OtpPurpose.Login, CreatedAt = DateTime.UtcNow });

        var response = await h.Auth.VerifyOtpAsync(new VerifyOtpRequest { PhoneNumber = Phone, Otp = "123456" });

        Assert.False(response.AccountRestored);
        Assert.DoesNotContain(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.AccountDeletionCancelled);
    }

    [Fact]
    public async Task Purge_DeletesDueAccounts_RemovesTheirObjects_AndLeavesOthersAlone()
    {
        var h = new Harness();
        var due = Guid.NewGuid();
        var notDue = Guid.NewGuid();
        h.Deletions.Due.Add(due);
        h.Deletions.Objects[due] = [new PurgedObject("bucket", "a.jpg"), new PurgedObject("bucket", "a.jpg"), new PurgedObject("bucket", "a-thumb.jpg")];
        h.Deletions.Phones[due] = "+15551112222";
        h.Deletions.Other.Add(notDue);

        var summary = await h.Service.PurgeDueAsync();

        Assert.Equal(1, summary.Deleted);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(["a.jpg", "a-thumb.jpg"], h.Storage.Deleted);
        Assert.Equal(["+15551112222"], h.Otp.Deleted);
        Assert.Contains(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.AccountDeleted && e.ActorId == due);
        Assert.Contains(notDue, h.Deletions.Other);
    }

    [Fact]
    public async Task Purge_SkipsAnAccountThatWasRestoredMeanwhile()
    {
        var h = new Harness();
        var id = Guid.NewGuid();
        h.Deletions.Due.Add(id);
        h.Deletions.NoLongerDue.Add(id);

        var summary = await h.Service.PurgeDueAsync();

        Assert.Equal(0, summary.Deleted);
        Assert.Equal(1, summary.Skipped);
        Assert.Empty(h.Storage.Deleted);
    }

    [Fact]
    public async Task Purge_ContinuesPastAFailingAccount()
    {
        var h = new Harness();
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        h.Deletions.Due.AddRange([bad, good]);
        h.Deletions.Failing.Add(bad);

        var summary = await h.Service.PurgeDueAsync();

        Assert.Equal(1, summary.Deleted);
        Assert.Equal(1, summary.Failed);
        Assert.Contains(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.AccountDeleteFailed && e.ActorId == bad);
    }

    private sealed class Harness
    {
        public Harness()
        {
            User = new User(Phone, "Sam", "Quiet Fox");
            Users = new FakeUsers(User);
            Sessions = new FakeSessions();
            Deletions = new FakeDeletions();
            Storage = new FakeObjectStorage();
            Otp = new FakeOtpStore();
            Audit = new RecordingAuditLogWriter();
            Service = new AccountDeletionService(Users, Sessions, Deletions, Storage, Otp, Audit);
            Auth = new AuthService(
                Users,
                Otp,
                new FixedOtpGenerator(),
                new FixedNameGenerator(),
                Sessions,
                new InlineValidator<RequestOtpRequest>(),
                new InlineValidator<ResendOtpRequest>(),
                new InlineValidator<VerifyOtpRequest>(),
                Audit);
        }

        public User User { get; }
        public FakeUsers Users { get; }
        public FakeSessions Sessions { get; }
        public FakeDeletions Deletions { get; }
        public FakeObjectStorage Storage { get; }
        public FakeOtpStore Otp { get; }
        public RecordingAuditLogWriter Audit { get; }
        public AccountDeletionService Service { get; }
        public AuthService Auth { get; }
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly List<User> _users;

        public FakeUsers(params User[] users) => _users = [.. users];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.FirstOrDefault(user => user.PhoneNumber == phoneNumber));

        public Task<IReadOnlyList<User>> ListAsync(UserStatus? status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>(_users);

        public Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSessions : ISessionService
    {
        public int RevokeAllCalls { get; private set; }

        public Task<TokenResponse> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TokenResponse { AccessToken = "access", RefreshToken = "refresh" });

        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RevokeAllCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeletions : IAccountDeletionRepository
    {
        public List<Guid> Due { get; } = [];
        public List<Guid> Other { get; } = [];
        public HashSet<Guid> NoLongerDue { get; } = [];
        public HashSet<Guid> Failing { get; } = [];
        public Dictionary<Guid, PurgedObject[]> Objects { get; } = [];
        public Dictionary<Guid, string> Phones { get; } = [];

        public Task<IReadOnlyList<Guid>> ListDueUserIdsAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>(Due.Take(batchSize).ToList());

        public Task<AccountPurgeResult> PurgeAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            if (Failing.Contains(userId))
            {
                throw new InvalidOperationException("db down");
            }

            if (NoLongerDue.Contains(userId))
            {
                return Task.FromResult(new AccountPurgeResult(false, null, []));
            }

            return Task.FromResult(new AccountPurgeResult(
                true,
                Phones.GetValueOrDefault(userId),
                Objects.GetValueOrDefault(userId) ?? []));
        }
    }

    private sealed class FakeOtpStore : IOtpChallengeStore
    {
        private readonly Dictionary<string, OtpChallenge> _challenges = [];

        public List<string> Deleted { get; } = [];

        public void Set(string phone, OtpChallenge challenge) => _challenges[phone] = challenge;

        public Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(_challenges.GetValueOrDefault(email));

        public Task SetAsync(string email, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _challenges[email] = challenge;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceKeepingTtlAsync(string email, OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            _challenges[email] = challenge;
            return Task.FromResult(true);
        }

        public Task DeleteAsync(string email, CancellationToken cancellationToken = default)
        {
            Deleted.Add(email);
            _challenges.Remove(email);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteIfChallengeMatchesAsync(string email, Guid challengeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FixedOtpGenerator : IOtpGenerator
    {
        public string Generate() => "123456";
    }

    private sealed class FixedNameGenerator : IDisplayNameGenerator
    {
        public string Generate() => "Quiet Fox";
    }
}
