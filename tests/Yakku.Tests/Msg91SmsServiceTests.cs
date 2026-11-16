using System.Net;
using System.Text.Json;
using Yakku.Application.Auth.Sms;
using Yakku.Infrastructure.Sms;

namespace Yakku.Tests;

/// <summary>The MSG91 integration against a stubbed HTTP handler. No real SMS is ever requested.</summary>
public class Msg91SmsServiceTests
{
    private const string Phone = "+919876543210";
    private const string Otp = "482913";
    private const string AuthKey = "test-authkey-do-not-leak-123";
    private const string FlowId = "flow-id-for-tests";

    private static Msg91Options Options() => Msg91Options.FromEnvironment(name => name switch
    {
        Msg91Options.AuthKeyVariable => AuthKey,
        Msg91Options.FlowIdVariable => FlowId,
        _ => null
    });

    private static (Msg91SmsService Service, StubHttpHandler Handler, CapturingLogger<Msg91SmsService> Log) Create(
        StubHttpHandler handler,
        TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://control.msg91.com/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        };
        var log = new CapturingLogger<Msg91SmsService>();
        return (new Msg91SmsService(client, Options(), log), handler, log);
    }

    private static SmsOtpMessage Message(string phone = Phone, int expiry = 5) =>
        new(phone, Otp, expiry, Guid.Parse("11111111-2222-3333-4444-555555555555"));

    private static StubHttpHandler Reply(HttpStatusCode status, string body) => StubHttpHandler.Replying(status, body);

    // ---- success and request shape ------------------------------------------------------------------------

    [Fact]
    public async Task ValidNumber_SendsTheFlowRequest_AndIsAcceptedOnTypeSuccess()
    {
        var (service, handler, _) = Create(Reply(HttpStatusCode.OK, """{"type":"success","message":"3366776d7a47313839303535"}"""));

        var result = await service.SendOtpAsync(Message());

        Assert.True(result.Accepted);
        Assert.Equal(SmsFailureCategory.None, result.Failure);
        Assert.Equal(200, result.ProviderHttpStatus);
        Assert.Equal("3366776d7a47313839303535", result.ProviderRequestId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://control.msg91.com/api/v5/flow", request.Uri!.ToString());
        Assert.Equal(AuthKey, request.Headers["authkey"]);
        Assert.Contains("application/json", request.Headers["Accept"]);
        Assert.Equal("application/json", request.ContentType);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(FlowId, body.RootElement.GetProperty("template_id").GetString());
        var recipient = Assert.Single(body.RootElement.GetProperty("recipients").EnumerateArray());
        Assert.Equal("919876543210", recipient.GetProperty("mobiles").GetString());
        Assert.Equal(Otp, recipient.GetProperty("otp").GetString());
        Assert.Equal("5", recipient.GetProperty("expiry").GetString());
    }

    [Fact]
    public async Task TheExpiryInTheSms_IsTheMinutesTheCallerSays()
    {
        var (service, handler, _) = Create(Reply(HttpStatusCode.OK, """{"type":"success","message":"id"}"""));

        await service.SendOtpAsync(Message(expiry: 10));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal("10", body.RootElement.GetProperty("recipients")[0].GetProperty("expiry").GetString());
    }

    [Theory]
    [InlineData("+919876543210", "919876543210")]
    [InlineData("+916000000001", "916000000001")]
    [InlineData("+917123456789", "917123456789")]
    [InlineData("+918999999999", "918999999999")]
    public async Task IndianNumbers_AreSentWithTheCountryCodeAndNoPlus(string e164, string expected)
    {
        var (service, handler, _) = Create(Reply(HttpStatusCode.OK, """{"type":"success","message":"id"}"""));

        await service.SendOtpAsync(Message(e164));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(expected, body.RootElement.GetProperty("recipients")[0].GetProperty("mobiles").GetString());
    }

    // ---- recipient validation ------------------------------------------------------------------------------

    [Theory]
    [InlineData("+15551234567")]      // not India
    [InlineData("+91987654321")]      // nine digits
    [InlineData("+9198765432101")]    // eleven digits
    [InlineData("+915987654321")]     // Indian mobiles start with 6-9
    [InlineData("9876543210")]        // no country code
    [InlineData("919876543210")]      // no plus
    [InlineData("+91 9876543210")]    // not normalised
    [InlineData("+91987654321a")]
    [InlineData("")]
    public async Task InvalidNumbers_AreRefusedWithoutCallingTheProvider(string phone)
    {
        var (service, handler, _) = Create(Reply(HttpStatusCode.OK, """{"type":"success","message":"id"}"""));

        var result = await service.SendOtpAsync(Message(phone));

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.InvalidRecipient, result.Failure);
        Assert.Empty(handler.Requests);
    }

    // ---- provider failures ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"type":"error","message":"Authentication failure","code":"401"}""")]
    [InlineData(HttpStatusCode.Forbidden, """{"type":"error","message":"forbidden"}""")]
    [InlineData(HttpStatusCode.OK, """{"type":"error","message":"Authentication failure","code":"401"}""")]
    [InlineData(HttpStatusCode.OK, """{"type":"error","message":"IP not whitelisted"}""")]
    public async Task AuthenticationFailure_IsReported_EvenInsideAnHttp200(HttpStatusCode status, string body)
    {
        var (service, _, _) = Create(Reply(status, body));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.Authentication, result.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"type":"error","message":"Template ID not found"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"type":"error","message":"Flow is not approved"}""")]
    [InlineData(HttpStatusCode.OK, """{"message":"Template variables mismatch","type":"error"}""")]
    public async Task TemplateOrFlowFailure_IsReported(HttpStatusCode status, string body)
    {
        var (service, _, _) = Create(Reply(status, body));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.Template, result.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, SmsFailureCategory.ProviderError)]
    [InlineData(HttpStatusCode.BadGateway, SmsFailureCategory.ProviderError)]
    [InlineData(HttpStatusCode.RequestTimeout, SmsFailureCategory.ProviderError)]
    [InlineData(HttpStatusCode.TooManyRequests, SmsFailureCategory.ProviderRateLimited)]
    [InlineData(HttpStatusCode.BadRequest, SmsFailureCategory.Rejected)]
    [InlineData(HttpStatusCode.NotFound, SmsFailureCategory.Rejected)]
    public async Task OtherHttpFailures_AreClassified(HttpStatusCode status, SmsFailureCategory expected)
    {
        var (service, _, _) = Create(Reply(status, """{"type":"error","message":"nope"}"""));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(expected, result.Failure);
        Assert.Equal((int)status, result.ProviderHttpStatus);
    }

    [Fact]
    public async Task AnErrorBodyInsideAnHttp200_IsNeverTreatedAsSuccess()
    {
        var (service, _, _) = Create(Reply(HttpStatusCode.OK, """{"type":"error","message":"Insufficient balance"}"""));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.Rejected, result.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("""{"message":"abc"}""")]
    [InlineData("""{"type":"pending","message":"abc"}""")]
    [InlineData("""{"type":"warning"}""")]
    public async Task AnHttp200_ThatDoesNotSaySuccess_IsNotAccepted(string body)
    {
        var (service, _, _) = Create(Reply(HttpStatusCode.OK, body));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.InvalidResponse, result.Failure);
    }

    [Fact]
    public async Task ATypeSuccessBody_OnAnErrorStatus_IsNotAccepted()
    {
        var (service, _, _) = Create(Reply(HttpStatusCode.InternalServerError, """{"type":"success","message":"id"}"""));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.ProviderError, result.Failure);
    }

    // ---- network failure, timeout, cancellation ------------------------------------------------------------

    [Fact]
    public async Task NetworkFailure_IsReported_NotThrown()
    {
        var (service, _, log) = Create(new StubHttpHandler((_, _) => throw new HttpRequestException("No such host is known.")));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.Network, result.Failure);
        Assert.Null(result.ProviderHttpStatus);
        Assert.Contains("Network", log.AllText);
    }

    [Fact]
    public async Task ATimeout_IsReported_AsATimeout()
    {
        // The handler never answers; HttpClient's own timeout fires.
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });
        var (service, _, _) = Create(handler, timeout: TimeSpan.FromMilliseconds(100));

        var result = await service.SendOtpAsync(Message());

        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.Timeout, result.Failure);
    }

    [Fact]
    public async Task WhenTheCallerCancels_TheCancellationPropagates_AndIsNotAProviderFailure()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });
        var (service, _, _) = Create(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendOtpAsync(Message(), cts.Token));
    }

    // ---- logging ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Logs_NeverContainTheAuthKey_TheOtp_ThePayload_OrTheFullNumber_OnSuccess()
    {
        var (service, _, log) = Create(Reply(HttpStatusCode.OK, """{"type":"success","message":"req-abc"}"""));

        await service.SendOtpAsync(Message());

        AssertNothingSensitiveLogged(log);
        Assert.Contains("11111111-2222-3333-4444-555555555555", log.AllText); // correlation id
        Assert.Contains("***10", log.AllText);                              // masked recipient
        Assert.Contains("200", log.AllText);                                 // provider status
        Assert.Contains("req-abc", log.AllText);                             // provider request id
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Logs_NeverContainSecrets_EvenWhenTheProviderEchoesThem(HttpStatusCode status)
    {
        // A hostile or sloppy provider message that repeats the credentials, the OTP and the number.
        var echo = $$"""{"type":"error","message":"Authentication failure: authkey {{AuthKey}} otp {{Otp}} mobile 919876543210 +919876543210","code":"401"}""";
        var (service, _, log) = Create(Reply(status, echo));

        await service.SendOtpAsync(Message());

        AssertNothingSensitiveLogged(log);
        Assert.Contains("Authentication", log.AllText); // the failure category is logged
    }

    [Fact]
    public async Task Logs_NeverContainSecrets_OnNetworkFailure_Timeout_AndInvalidRecipient()
    {
        var (network, _, networkLog) = Create(new StubHttpHandler((_, _) => throw new HttpRequestException("connection reset")));
        await network.SendOtpAsync(Message());

        var (timeout, _, timeoutLog) = Create(
            new StubHttpHandler(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException();
            }),
            TimeSpan.FromMilliseconds(50));
        await timeout.SendOtpAsync(Message());

        var (invalid, _, invalidLog) = Create(Reply(HttpStatusCode.OK, "{}"));
        await invalid.SendOtpAsync(Message("+15551234567"));

        AssertNothingSensitiveLogged(networkLog);
        AssertNothingSensitiveLogged(timeoutLog);
        AssertNothingSensitiveLogged(invalidLog);
    }

    [Fact]
    public void TheMessage_AndTheOptions_DoNotRevealSecretsWhenPrinted()
    {
        Assert.DoesNotContain(Otp, Message().ToString());
        Assert.DoesNotContain(Phone, Message().ToString());
        Assert.DoesNotContain(AuthKey, Options().ToString());
        Assert.DoesNotContain(FlowId, Options().ToString());
    }

    [Fact]
    public async Task TheDisabledService_SendsNothing_AndSaysSo()
    {
        var service = new DisabledSmsService();

        var result = await service.SendOtpAsync(Message());

        Assert.False(service.IsEnabled);
        Assert.False(result.Accepted);
        Assert.Equal(SmsFailureCategory.Disabled, result.Failure);
    }

    private static void AssertNothingSensitiveLogged(CapturingLogger<Msg91SmsService> log)
    {
        var text = log.AllText;
        Assert.NotEmpty(log.Entries);
        Assert.DoesNotContain(AuthKey, text);
        Assert.DoesNotContain(Otp, text);
        Assert.DoesNotContain("9876543210", text);          // the number, with or without the country code
        Assert.DoesNotContain("template_id", text);          // the request payload
        Assert.DoesNotContain("recipients", text);
        Assert.DoesNotContain(FlowId, text);
    }
}
