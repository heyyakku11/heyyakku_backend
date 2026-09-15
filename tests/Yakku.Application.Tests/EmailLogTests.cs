using Yakku.Application.Auth.Models;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.Email;

public class EmailLogTests
{
    [Fact]
    public void Lifecycle_QueuedToSendingToSent()
    {
        var log = CreateLog();

        log.MarkSending();
        log.RecordAttempt();
        log.MarkSent("re_123");

        Assert.Equal(EmailStatus.Sent, log.Status);
        Assert.Equal(1, log.AttemptCount);
        Assert.Equal("re_123", log.ProviderMessageId);
        Assert.NotNull(log.StartedAt);
        Assert.NotNull(log.SentAt);
    }

    [Fact]
    public void Lifecycle_RetriesStayInSending_ThenFailed()
    {
        var log = CreateLog();

        log.MarkSending();
        log.RecordAttempt();
        log.RecordAttempt();
        log.RecordAttempt();
        log.MarkFailed("Http500", "provider error");

        Assert.Equal(EmailStatus.Failed, log.Status);
        Assert.Equal(3, log.AttemptCount);
        Assert.Equal("Http500", log.ErrorCode);
        Assert.NotNull(log.FailedAt);
    }

    [Fact]
    public void MarkCancelled_FromQueued_DoesNotRequireSending()
    {
        var log = CreateLog();

        log.MarkCancelled("Superseded", "replaced");

        Assert.Equal(EmailStatus.Cancelled, log.Status);
        Assert.Equal("Superseded", log.ErrorCode);
        Assert.NotNull(log.FailedAt);
    }

    [Fact]
    public void EmailSendResult_ClassifiesOutcomes()
    {
        Assert.True(EmailSendResult.Success("id").Succeeded);
        Assert.True(EmailSendResult.TransientFailure("Http429", "slow").IsTransientFailure);
        Assert.True(EmailSendResult.PermanentFailure("Http422", "bad").IsPermanentFailure);
    }

    private static EmailLog CreateLog()
    {
        return new EmailLog(
            "user@example.com",
            EmailType.Otp,
            Guid.NewGuid(),
            "Resend",
            maxAttempts: 3);
    }
}
