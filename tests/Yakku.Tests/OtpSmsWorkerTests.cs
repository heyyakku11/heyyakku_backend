using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Sms;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Sms;

namespace Yakku.Tests;

public sealed class OtpSmsWorkerTests
{
    [Fact]
    public async Task SupersededChallenge_IsSkipped_WithoutCallingSms()
    {
        var store = new InMemoryOtpStore();
        var sms = new FakeSmsService();
        var audit = new ThreadSafeAuditWriter();
        var channel = Channel.CreateBounded<SmsOtpJob>(8);
        var worker = new OtpSmsWorker(
            channel,
            new FixedStoreScopeFactory(store),
            sms,
            NullLogger<OtpSmsWorker>.Instance,
            audit);

        var job = new SmsOtpJob(Guid.NewGuid(), "+919876543210", "123456");
        await store.SetAsync(
            job.PhoneNumber,
            new OtpChallenge
            {
                ChallengeId = Guid.NewGuid(),
                OtpHash = "other",
                Purpose = OtpPurpose.Registration,
                CreatedAt = DateTime.UtcNow
            },
            TimeSpan.FromMinutes(5));

        await worker.ProcessJobAsync(job, CancellationToken.None);

        Assert.Empty(sms.Sent);
        var skipped = Assert.Single(audit.Notifications);
        Assert.Equal(NotificationAuditStatus.Skipped, skipped.Status);
        Assert.Equal("superseded", skipped.ErrorCode);
    }

    [Fact]
    public async Task RetryableFailure_IsRetriedImmediately_ThenRevokes()
    {
        var store = new InMemoryOtpStore();
        var sms = new FakeSmsService { Behavior = FakeSmsService.FailsWith(SmsFailureCategory.Network) };
        var audit = new ThreadSafeAuditWriter();
        var channel = Channel.CreateBounded<SmsOtpJob>(8);
        var worker = new OtpSmsWorker(
            channel,
            new FixedStoreScopeFactory(store),
            sms,
            NullLogger<OtpSmsWorker>.Instance,
            audit);

        var challengeId = Guid.NewGuid();
        var job = new SmsOtpJob(challengeId, "+919876543210", "123456");
        await store.SetAsync(
            job.PhoneNumber,
            new OtpChallenge
            {
                ChallengeId = challengeId,
                OtpHash = "hash",
                Purpose = OtpPurpose.Login,
                CreatedAt = DateTime.UtcNow
            },
            TimeSpan.FromMinutes(5));

        await worker.ProcessJobAsync(job, CancellationToken.None);

        Assert.Equal(OtpSmsQueueOptions.MaxAttempts, sms.Sent.Count);
        Assert.Null(await store.GetAsync(job.PhoneNumber));
        Assert.Equal(OtpSmsQueueOptions.MaxAttempts, audit.Notifications.Count);
        Assert.False(audit.Notifications.Last().WillRetry);
    }

    private sealed class FixedStoreScopeFactory : IServiceScopeFactory
    {
        private readonly IOtpChallengeStore _store;

        public FixedStoreScopeFactory(IOtpChallengeStore store) => _store = store;

        public IServiceScope CreateScope() => new Scope(_store);

        private sealed class Scope : IServiceScope, IServiceProvider
        {
            private readonly IOtpChallengeStore _store;

            public Scope(IOtpChallengeStore store) => _store = store;

            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType)
                => serviceType == typeof(IOtpChallengeStore) ? _store : null;

            public void Dispose()
            {
            }
        }
    }

    private sealed class InMemoryOtpStore : IOtpChallengeStore
    {
        private OtpChallenge? _challenge;
        private string? _phone;

        public Task<OtpChallenge?> GetAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_phone == phoneNumber ? _challenge : null);

        public Task SetAsync(string phoneNumber, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _phone = phoneNumber;
            _challenge = challenge;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceKeepingTtlAsync(string phoneNumber, OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            if (_phone != phoneNumber || _challenge is null)
            {
                return Task.FromResult(false);
            }

            _challenge = challenge;
            return Task.FromResult(true);
        }

        public Task DeleteAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            if (_phone == phoneNumber)
            {
                _phone = null;
                _challenge = null;
            }

            return Task.CompletedTask;
        }

        public Task<bool> DeleteIfChallengeMatchesAsync(string phoneNumber, Guid challengeId, CancellationToken cancellationToken = default)
        {
            if (_phone == phoneNumber && _challenge?.ChallengeId == challengeId)
            {
                _phone = null;
                _challenge = null;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<OtpIssueResult> TryIssueAsync(
            string phoneNumber,
            OtpChallenge challenge,
            OtpPolicy policy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OtpVerificationResult> VerifyAndConsumeAsync(
            string phoneNumber,
            string otpHash,
            int maxAttempts,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RevokeUndeliveredAsync(
            string phoneNumber,
            Guid challengeId,
            CancellationToken cancellationToken = default)
            => DeleteIfChallengeMatchesAsync(phoneNumber, challengeId, cancellationToken);
    }
}
