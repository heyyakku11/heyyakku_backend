using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Sms;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>A stand-in for MSG91 at the <see cref="ISmsService"/> seam. Records what it was asked to send; sends nothing.</summary>
internal sealed class FakeSmsService : ISmsService
{
    private readonly ConcurrentQueue<SmsOtpMessage> _sent = new();

    public bool IsEnabled { get; set; } = true;

    public static readonly Func<SmsOtpMessage, Task<SmsSendResult>> Accepts =
        _ => Task.FromResult(SmsSendResult.Success(200, "req-1", 5));

    /// <summary>What the next sends return. Defaults to the provider accepting everything.</summary>
    public Func<SmsOtpMessage, Task<SmsSendResult>> Behavior { get; set; } = Accepts;

    public IReadOnlyCollection<SmsOtpMessage> Sent => _sent.ToArray();

    public SmsOtpMessage Last => _sent.Last();

    public Task<SmsSendResult> SendOtpAsync(SmsOtpMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Behavior(message);
    }

    public static Func<SmsOtpMessage, Task<SmsSendResult>> FailsWith(SmsFailureCategory failure) =>
        _ => Task.FromResult(SmsSendResult.Failed(failure, 200, 7));
}

/// <summary>Keeps everything a component logs, in every form it could leak through, so tests can search it.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries.ToArray();

    /// <summary>All log output as one string: formatted messages, structured values and exception text.</summary>
    public string AllText => string.Join('\n', _entries);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var parts = new List<string> { logLevel.ToString(), formatter(state, exception) };
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            parts.AddRange(values.Select(pair => $"{pair.Key}={pair.Value}"));
        }

        if (exception is not null)
        {
            parts.Add(exception.ToString());
        }

        _entries.Enqueue(string.Join(" | ", parts));
    }
}

/// <summary>An audit sink that is safe to call from the parallel requests of the concurrency tests.</summary>
internal sealed class ThreadSafeAuditWriter : IAuditLogWriter
{
    private readonly ConcurrentQueue<NotificationAuditEntry> _notifications = new();
    private readonly ConcurrentQueue<AuthenticationAuditEntry> _authentication = new();

    public IReadOnlyCollection<NotificationAuditEntry> Notifications => _notifications.ToArray();

    public IReadOnlyCollection<AuthenticationAuditEntry> Authentication => _authentication.ToArray();

    public Task WriteAuthenticationAsync(AuthenticationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        _authentication.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task WritePollAsync(PollAuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task WriteNotificationAsync(NotificationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        _notifications.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task WriteSystemAsync(SystemLogEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> RecordNotificationDeliveryAsync(
        NotificationChannel channel,
        string provider,
        string providerMessageId,
        NotificationDeliveryStatus deliveryStatus,
        DateTime occurredAtUtc,
        string? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
}

/// <summary>An HttpMessageHandler that answers from a delegate and records every request, body included.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public IReadOnlyCollection<RecordedRequest> Requests => _requests.ToArray();

    public static StubHttpHandler Replying(System.Net.HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase),
            request.Content?.Headers.ContentType?.MediaType,
            body));
        return await _respond(request, cancellationToken);
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? ContentType,
    string Body);
