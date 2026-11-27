using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Yakku.Application;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Yakku.API.AskBack;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>
/// Runs the real hub, JWT bearer setup, presence and real-time publisher in a Kestrel host on a free port, with the
/// real service and repository on PostgreSQL, and connects real SignalR clients to it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AskBackHubTests
{
    private const string Secret = "test-signing-key-test-signing-key-0123456789";
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public AskBackHubTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Connecting_WithoutAValidToken_IsRefused()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();

        await using var anonymous = host.Connect(token: null);
        await using var forged = host.Connect(token: "not-a-jwt");

        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync());
        await Assert.ThrowsAnyAsync<Exception>(() => forged.StartAsync());
    }

    [SkippableFact]
    public async Task Connecting_WithALoggedOutSession_IsRefused()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var user = await host.AddUserAsync();
        host.Sessions.Revoke(user.SessionId);

        await using var connection = host.Connect(user);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await connection.StartAsync();
            // The server aborts a connection that failed the session check.
            await Task.Delay(500);
            await connection.InvokeAsync("JoinConversation", Guid.NewGuid());
        });
    }

    [SkippableFact]
    public async Task OnlyParticipants_CanJoin_AGuessedConversationIdIsRefused()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        var outsider = await host.AddUserAsync();

        await using var stranger = host.Connect(outsider);
        await stranger.StartAsync();
        var refused = await Assert.ThrowsAsync<HubException>(
            () => stranger.InvokeAsync("JoinConversation", conversationId));
        Assert.Contains("not found", refused.Message, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<HubException>(
            () => stranger.InvokeAsync("JoinConversation", Guid.NewGuid()));

        // The refused outsider is not present, and cannot type into it either.
        Assert.False(host.Presence.IsViewing(conversationId, outsider.UserId));
        await Assert.ThrowsAsync<HubException>(() => stranger.InvokeAsync("TypingStarted", conversationId));

        await using var participant = host.Connect(asker);
        await participant.StartAsync();
        await participant.InvokeAsync("JoinConversation", conversationId);
        Assert.True(host.Presence.IsViewing(conversationId, asker.UserId));
        Assert.False(host.Presence.IsViewing(conversationId, voter.UserId));
    }

    [SkippableFact]
    public async Task SentMessage_ReachesBothParticipants_WithoutExposingIdentity()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();

        await using var askerConnection = host.Connect(asker);
        await using var voterConnection = host.Connect(voter);
        // A second device of the asker also hears it.
        await using var askerTablet = host.Connect(asker);
        var askerEvents = Listen<AskBackMessageResponse>(askerConnection, "ReceiveMessage");
        var tabletEvents = Listen<AskBackMessageResponse>(askerTablet, "ReceiveMessage");
        var voterEvents = Listen<AskBackMessageResponse>(voterConnection, "ReceiveMessage");
        await askerConnection.StartAsync();
        await askerTablet.StartAsync();
        await voterConnection.StartAsync();
        await voterConnection.InvokeAsync("JoinConversation", conversationId);

        var sent = await host.SendAsync(asker, conversationId, "Why did you pick that?");

        var atVoter = await Next(voterEvents);
        var atAsker = await Next(askerEvents);
        var atTablet = await Next(tabletEvents);
        Assert.Equal(sent.Id, atVoter.Id);
        Assert.False(atVoter.IsMine);
        Assert.Equal("Why did you pick that?", atVoter.Content);
        Assert.Equal(asker.AnonymousName, atVoter.SenderName);
        Assert.True(atAsker.IsMine);
        Assert.True(atTablet.IsMine);

        var json = System.Text.Json.JsonSerializer.Serialize(atVoter);
        Assert.DoesNotContain(asker.UserId.ToString(), json);
        Assert.DoesNotContain(asker.Phone, json);
    }

    [SkippableFact]
    public async Task MessageFromANonJoinedConversation_NeverReachesAnOutsider()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, _, conversationId) = await host.StartConversationAsync();
        var outsider = await host.AddUserAsync();
        await using var outsiderConnection = host.Connect(outsider);
        var outsiderEvents = Listen<AskBackMessageResponse>(outsiderConnection, "ReceiveMessage");
        await outsiderConnection.StartAsync();

        await host.SendAsync(asker, conversationId, "private");

        Assert.False(await outsiderEvents.Reader.WaitToReadAsync(CancellationToken.None).AsTask()
            .WaitAsync(TimeSpan.FromMilliseconds(800)).ContinueWith(t => t.IsCompletedSuccessfully && t.Result));
    }

    [SkippableFact]
    public async Task Typing_IsRelayedToTheOtherParticipantOnly_AfterJoining()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var askerConnection = host.Connect(asker);
        await using var voterConnection = host.Connect(voter);
        var voterTyping = Listen<TypingEvent>(voterConnection, "UserTyping");
        var voterStopped = Listen<TypingEvent>(voterConnection, "UserStoppedTyping");
        var askerTyping = Listen<TypingEvent>(askerConnection, "UserTyping");
        await askerConnection.StartAsync();
        await voterConnection.StartAsync();

        // Before joining, typing is refused.
        await Assert.ThrowsAsync<HubException>(() => askerConnection.InvokeAsync("TypingStarted", conversationId));

        await askerConnection.InvokeAsync("JoinConversation", conversationId);
        await voterConnection.InvokeAsync("JoinConversation", conversationId);
        await askerConnection.InvokeAsync("TypingStarted", conversationId);
        await askerConnection.InvokeAsync("TypingStopped", conversationId);

        Assert.Equal(conversationId, (await Next(voterTyping)).ConversationId);
        Assert.Equal(conversationId, (await Next(voterStopped)).ConversationId);
        // The typist does not hear themselves.
        Assert.False(askerTyping.Reader.TryRead(out _));
    }

    [SkippableFact]
    public async Task ReadStatusAndClosing_AreDeliveredToTheOtherParticipant()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var askerConnection = host.Connect(asker);
        var reads = Listen<ReadEvent>(askerConnection, "MessageRead");
        var closes = Listen<StatusEvent>(askerConnection, "ConversationClosed");
        var reopens = Listen<StatusEvent>(askerConnection, "ConversationReopened");
        await askerConnection.StartAsync();

        var message = await host.SendAsync(asker, conversationId, "read me");
        await host.WithService(service => service.MarkReadAsync(
            voter.UserId, conversationId, new MarkAskBackReadRequest { LastReadMessageId = message.Id }));
        await host.WithService(service => service.CloseAsync(voter.UserId, conversationId));
        await host.WithService(service => service.ReopenAsync(asker.UserId, conversationId));

        var read = await Next(reads);
        Assert.Equal(message.Id, read.LastReadMessageId);
        Assert.Equal("closed", (await Next(closes)).Status);
        Assert.Equal("active", (await Next(reopens)).Status);
    }

    [SkippableFact]
    public async Task AfterReconnecting_MissedMessagesAreRecoveredFromTheHistory()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var first = host.Connect(voter);
        var firstEvents = Listen<AskBackMessageResponse>(first, "ReceiveMessage");
        await first.StartAsync();
        await first.InvokeAsync("JoinConversation", conversationId);
        var seen = await host.SendAsync(asker, conversationId, "seen live");
        Assert.Equal(seen.Id, (await Next(firstEvents)).Id);
        await first.StopAsync();
        // The server notices the disconnect a moment after the client closes.
        for (var i = 0; i < 50 && host.Presence.IsViewing(conversationId, voter.UserId); i++)
        {
            await Task.Delay(100);
        }

        Assert.False(host.Presence.IsViewing(conversationId, voter.UserId));

        // Offline: these two are only stored (the people take turns: the voter answers, the asker follows up).
        await host.SendAsync(voter, conversationId, "missed 1");
        await host.SendAsync(asker, conversationId, "missed 2");

        await using var second = host.Connect(voter);
        var secondEvents = Listen<AskBackMessageResponse>(second, "ReceiveMessage");
        await second.StartAsync();
        await second.InvokeAsync("JoinConversation", conversationId);
        var missed = await host.WithService(service => service.ListMessagesAsync(
            voter.UserId, conversationId, seen.Cursor, "newer", null));

        Assert.Equal(["missed 1", "missed 2"], missed.Items.Select(m => m.Content));
        Assert.False(missed.HasMore);

        // A message arriving now is delivered live, and the client can drop it if it already fetched it.
        var live = await host.SendAsync(voter, conversationId, "live again");
        Assert.Equal(live.Id, (await Next(secondEvents)).Id);
    }

    // ---- delivery acknowledgments ----

    [SkippableFact]
    public async Task ReceivedMessage_AcknowledgedOverTheHub_IsDelivered()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var voterConnection = host.Connect(voter, deliveryAck: true);
        var voterEvents = Listen<AskBackMessageResponse>(voterConnection, "ReceiveMessage");
        await voterConnection.StartAsync();
        await host.WaitForAckCapableAsync(voter);

        var sent = await host.SendAsync(asker, conversationId, "ack me");
        Assert.Equal(AskBackDeliveryStatus.Sent, await DeliveryStatusAsync(sent.Id));
        var received = await Next(voterEvents);
        await voterConnection.InvokeAsync("AcknowledgeMessage", received.Id);

        var row = await DeliveryAsync(sent.Id);
        Assert.Equal(AskBackDeliveryStatus.Delivered, row.Status);
        Assert.True(row.DeliveredAt >= row.SentAt);
    }

    [SkippableFact]
    public async Task Acknowledgment_FromAnyoneButTheRecipient_IsRefused()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        var outsider = await host.AddUserAsync();
        await using var voterConnection = host.Connect(voter, deliveryAck: true);
        await using var askerConnection = host.Connect(asker, deliveryAck: true);
        await using var outsiderConnection = host.Connect(outsider, deliveryAck: true);
        await voterConnection.StartAsync();
        await askerConnection.StartAsync();
        await outsiderConnection.StartAsync();
        await host.WaitForAckCapableAsync(voter);

        var sent = await host.SendAsync(asker, conversationId, "not yours to ack");

        // The sender, a stranger and a made-up id all get the same answer.
        var bySender = await Assert.ThrowsAsync<HubException>(() => askerConnection.InvokeAsync("AcknowledgeMessage", sent.Id));
        var byStranger = await Assert.ThrowsAsync<HubException>(() => outsiderConnection.InvokeAsync("AcknowledgeMessage", sent.Id));
        var unknown = await Assert.ThrowsAsync<HubException>(() => voterConnection.InvokeAsync("AcknowledgeMessage", Guid.NewGuid()));
        Assert.All(new[] { bySender, byStranger, unknown }, e => Assert.Contains("Message not found", e.Message));
        Assert.Equal(AskBackDeliveryStatus.Sent, await DeliveryStatusAsync(sent.Id));
    }

    [SkippableFact]
    public async Task Acknowledgment_FromALoggedOutSession_IsRefused()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var voterConnection = host.Connect(voter, deliveryAck: true);
        var voterEvents = Listen<AskBackMessageResponse>(voterConnection, "ReceiveMessage");
        await voterConnection.StartAsync();
        await host.WaitForAckCapableAsync(voter);
        var sent = await host.SendAsync(asker, conversationId, "then logged out");
        await Next(voterEvents);

        host.Sessions.Revoke(voter.SessionId);

        await Assert.ThrowsAnyAsync<Exception>(() => voterConnection.InvokeAsync("AcknowledgeMessage", sent.Id));
        Assert.Equal(AskBackDeliveryStatus.Sent, await DeliveryStatusAsync(sent.Id));
    }

    [SkippableFact]
    public async Task ReconnectedClient_CanAcknowledgeAMessageReceivedBeforeTheDrop()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var first = host.Connect(voter, deliveryAck: true);
        var firstEvents = Listen<AskBackMessageResponse>(first, "ReceiveMessage");
        await first.StartAsync();
        await host.WaitForAckCapableAsync(voter);
        var sent = await host.SendAsync(asker, conversationId, "received, ack lost");
        Assert.Equal(sent.Id, (await Next(firstEvents)).Id);

        // The connection drops before the acknowledgment goes out.
        await first.StopAsync();
        await using var second = host.Connect(voter, deliveryAck: true);
        await second.StartAsync();
        await second.InvokeAsync("AcknowledgeMessage", sent.Id);

        Assert.Equal(AskBackDeliveryStatus.Delivered, await DeliveryStatusAsync(sent.Id));
    }

    [SkippableFact]
    public async Task DuplicateDeliveryToTwoDevices_IsAcknowledgedOnce_AndBothAcknowledgmentsSucceed()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        await using var phone = host.Connect(voter, deliveryAck: true);
        await using var tablet = host.Connect(voter, deliveryAck: true);
        var phoneEvents = Listen<AskBackMessageResponse>(phone, "ReceiveMessage");
        var tabletEvents = Listen<AskBackMessageResponse>(tablet, "ReceiveMessage");
        await phone.StartAsync();
        await tablet.StartAsync();
        await host.WaitForAckCapableAsync(voter);

        var sent = await host.SendAsync(asker, conversationId, "twice");
        Assert.Equal(sent.Id, (await Next(phoneEvents)).Id);
        Assert.Equal(sent.Id, (await Next(tabletEvents)).Id);
        await Task.WhenAll(
            phone.InvokeAsync("AcknowledgeMessage", sent.Id),
            tablet.InvokeAsync("AcknowledgeMessage", sent.Id));
        var deliveredAt = (await DeliveryAsync(sent.Id)).DeliveredAt;
        await phone.InvokeAsync("AcknowledgeMessage", sent.Id);

        var row = await DeliveryAsync(sent.Id);
        Assert.Equal(AskBackDeliveryStatus.Delivered, row.Status);
        Assert.Equal(deliveredAt, row.DeliveredAt);
    }

    [SkippableFact]
    public async Task ConnectionWithoutTheAckFlag_IsTrackedAsNoAckConnection()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var host = await HostAsync();
        var (asker, voter, conversationId) = await host.StartConversationAsync();
        // An app version from before acknowledgments: same event, no flag.
        await using var oldApp = host.Connect(voter);
        var events = Listen<AskBackMessageResponse>(oldApp, "ReceiveMessage");
        await oldApp.StartAsync();
        await oldApp.InvokeAsync("JoinConversation", conversationId);

        var sent = await host.SendAsync(asker, conversationId, "old client");

        Assert.Equal(sent.Id, (await Next(events)).Id);
        Assert.False(host.Presence.HasAckCapableConnection(voter.UserId));
        Assert.Equal(AskBackDeliveryStatus.NoAckConnection, await DeliveryStatusAsync(sent.Id));
    }

    private async Task<AskBackMessageDeliveryTracking> DeliveryAsync(Guid messageId)
    {
        await using var context = _fixture.CreateContext();
        return await context.AskBackDeliveries.AsNoTracking().SingleAsync(d => d.MessageId == messageId);
    }

    private async Task<AskBackDeliveryStatus> DeliveryStatusAsync(Guid messageId)
    {
        return (await DeliveryAsync(messageId)).Status;
    }

    // ---- test host ----

    private sealed record TypingEvent(Guid ConversationId);

    private sealed record ReadEvent(Guid ConversationId, Guid LastReadMessageId);

    private sealed record StatusEvent(Guid ConversationId, string Status);

    private sealed record TestUser(Guid UserId, Guid SessionId, string AnonymousName, string Phone)
    {
    }

    private static Channel<T> Listen<T>(HubConnection connection, string eventName)
    {
        var channel = Channel.CreateUnbounded<T>();
        connection.On<T>(eventName, item => channel.Writer.TryWrite(item));
        return channel;
    }

    private static async Task<T> Next<T>(Channel<T> channel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await channel.Reader.ReadAsync(timeout.Token);
    }

    private async Task<TestHost> HostAsync()
    {
        var host = new TestHost(_fixture);
        await host.StartAsync();
        return host;
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly PostgresFixture _fixture;
        private WebApplication _app = null!;

        public TestHost(PostgresFixture fixture)
        {
            _fixture = fixture;
        }

        public FakeSessions Sessions { get; } = new();
        public AskBackPresence Presence { get; private set; } = null!;
        private string Url { get; set; } = string.Empty;

        public async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDbContext<YakkuDbContext>(options => options.UseNpgsql(_fixture.ConnectionString));
            builder.Services.AddApplication();
            builder.Services.AddScoped<IAskBackRepository, AskBackRepository>();
            builder.Services.AddScoped<Yakku.Application.Notifications.Interfaces.INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<Yakku.Application.Entitlements.IEntitlementRepository, EntitlementRepository>();
            builder.Services.AddSingleton<IUserSessionRepository>(Sessions);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret))
                };
                // Same as Program.cs: hub paths take the token from the query string.
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
            builder.Services.AddAuthorization();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, YakkuUserIdProvider>();
            builder.Services.AddSingleton<AskBackPresence>();
            builder.Services.AddSingleton<IAskBackPresence>(sp => sp.GetRequiredService<AskBackPresence>());
            builder.Services.AddSingleton<IAskBackRealtime, AskBackRealtime>();

            _app = builder.Build();
            _app.UseAuthentication();
            _app.UseAuthorization();
            _app.MapHub<AskBackHub>("/hubs/ask-back");
            await _app.StartAsync();
            Url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            Presence = _app.Services.GetRequiredService<AskBackPresence>();
        }

        public HubConnection Connect(TestUser user, bool deliveryAck = false)
        {
            return Connect(Token(user), deliveryAck);
        }

        public HubConnection Connect(string? token, bool deliveryAck = false)
        {
            var query = deliveryAck ? $"?{AskBackHub.DeliveryAckQueryKey}=1" : string.Empty;
            return new HubConnectionBuilder()
                .WithUrl($"{Url}/hubs/ask-back{query}", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    if (token is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                })
                .Build();
        }

        /// <summary>The handshake completes before the server's OnConnectedAsync registers the connection.</summary>
        public async Task WaitForAckCapableAsync(TestUser user)
        {
            for (var i = 0; i < 50 && !Presence.HasAckCapableConnection(user.UserId); i++)
            {
                await Task.Delay(20);
            }

            Assert.True(Presence.HasAckCapableConnection(user.UserId));
        }

        public async Task<TestUser> AddUserAsync()
        {
            await using var context = _fixture.CreateContext();
            var sequence = Interlocked.Increment(ref _sequence);
            var user = new User($"572{sequence:D10}", $"hub-anon-{sequence}");
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return new TestUser(user.Id, Guid.NewGuid(), user.AnonymousName, user.PhoneNumber);
        }

        public async Task<(TestUser Asker, TestUser Voter, Guid ConversationId)> StartConversationAsync()
        {
            var asker = await AddUserAsync();
            var voter = await AddUserAsync();
            await using var context = _fixture.CreateContext();
            var poll = new Poll(asker.UserId, "Hub question?", PollContentType.Question);
            poll.AddTextOption("Yes", 1);
            poll.AddTextOption("No", 2);
            poll.MarkPublished();
            await new PollRepository(context).AddAsync(poll);
            await context.SaveChangesAsync();
            var vote = PollVote.ForOption(voter.UserId, poll.Id, poll.Options.First().Id, "because");
            context.PollVotes.Add(vote);

            // Reasons are revealed (and Ask Back opens) once three other people answered.
            for (var i = 0; i < 2; i++)
            {
                var filler = await AddUserAsync();
                context.PollVotes.Add(PollVote.ForOption(filler.UserId, poll.Id, poll.Options.Last().Id, reason: null));
            }

            // The asker is on yakku_love so the chat has no question limit.
            var askerRow = await context.Users.FirstAsync(user => user.Id == asker.UserId);
            var love = await context.Plans.FirstAsync(plan => plan.Code == Plan.YakkuLoveCode);
            askerRow.ActivatePlan(love, DateTime.UtcNow);
            await context.SaveChangesAsync();

            var (conversation, _) = await WithService(service => service.CreateConversationAsync(
                asker.UserId, new CreateAskBackConversationRequest { VoteId = vote.Id }));
            return (asker, voter, conversation.Id);
        }

        public Task<AskBackMessageResponse> SendAsync(TestUser sender, Guid conversationId, string content)
        {
            return WithService(service => service.SendMessageAsync(
                sender.UserId,
                conversationId,
                new SendAskBackMessageRequest { ClientMessageId = Guid.NewGuid(), Content = content }));
        }

        public async Task<T> WithService<T>(Func<IAskBackService, Task<T>> action)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<IAskBackService>());
        }

        private string Token(TestUser user)
        {
            Sessions.Register(user.SessionId, user.UserId);
            var token = new JwtSecurityToken(
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                    new Claim(AuthClaimTypes.SessionId, user.SessionId.ToString())
                ],
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>Only the session check of the real repository; everything else is unused by the hub.</summary>
    public sealed class FakeSessions : IUserSessionRepository
    {
        private readonly Dictionary<Guid, Guid> _live = new();
        private readonly HashSet<Guid> _revoked = new();

        public void Register(Guid sessionId, Guid userId)
        {
            lock (_live)
            {
                if (!_revoked.Contains(sessionId))
                {
                    _live[sessionId] = userId;
                }
            }
        }

        public void Revoke(Guid sessionId)
        {
            lock (_live)
            {
                _revoked.Add(sessionId);
                _live.Remove(sessionId);
            }
        }

        public Task<bool> IsLiveAsync(Guid sessionId, Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            lock (_live)
            {
                return Task.FromResult(_live.TryGetValue(sessionId, out var owner) && owner == userId);
            }
        }

        public Task AddAsync(UserSession session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserSession>> ListLiveByUserIdAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserSession>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasLiveSessionForDeviceAsync(Guid deviceId, DateTime utcNow, Guid? exceptSessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LockUserAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryRotateAsync(Guid sessionId, string expectedHash, string newHash, DateTime newExpiresAt, DateTime utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteEndedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(UserSession session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
