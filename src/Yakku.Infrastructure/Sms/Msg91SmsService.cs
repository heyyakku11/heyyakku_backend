using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Sms;

namespace Yakku.Infrastructure.Sms
{
    /// <summary>
    /// Sends an OTP through the MSG91 Flow API. One attempt, no retry. A request counts as accepted only when
    /// MSG91 answers 2xx <b>and</b> says <c>"type":"success"</c>: it reports many failures (bad template, bad
    /// credentials) inside a 200 reply. Logs carry the correlation id, masked number, HTTP status and failure
    /// category; never the AuthKey, the OTP, the request body or a full number.
    /// </summary>
    public sealed class Msg91SmsService : ISmsService
    {
        public const string FlowPath = "api/v5/flow";
        public const string AuthKeyHeader = "authkey";

        private const int MaxProviderRequestIdLength = 64;
        private const int MaxLoggedProviderMessageLength = 120;

        private readonly HttpClient _httpClient;
        private readonly Msg91Options _options;
        private readonly ILogger<Msg91SmsService> _logger;

        public Msg91SmsService(HttpClient httpClient, Msg91Options options, ILogger<Msg91SmsService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;
        }

        public bool IsEnabled => true;

        public async Task<SmsSendResult> SendOtpAsync(SmsOtpMessage message, CancellationToken cancellationToken = default)
        {
            var recipient = AuditSanitizer.MaskRecipient(message.PhoneNumber);

            if (!IndianMobileNumber.TryFormatForMsg91(message.PhoneNumber, out var mobiles))
            {
                _logger.LogWarning(
                    "SMS OTP not sent: recipient {Recipient} is not a deliverable Indian mobile number. Correlation {CorrelationId}.",
                    recipient,
                    message.CorrelationId);
                return SmsSendResult.Failed(SmsFailureCategory.InvalidRecipient);
            }

            _logger.LogInformation(
                "SMS OTP request initiated. Correlation {CorrelationId}, recipient {Recipient}.",
                message.CorrelationId,
                recipient);

            using var request = BuildRequest(message, mobiles);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                stopwatch.Stop();
                return Interpret(message, recipient, response.StatusCode, body, (int)stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller gave up (for example the client disconnected); that is not a provider failure.
                throw;
            }
            catch (OperationCanceledException)
            {
                // HttpClient.Timeout elapsed.
                _logger.LogWarning(
                    "SMS provider did not answer in time. Correlation {CorrelationId}, recipient {Recipient}, failure {Failure}, latencyMs {LatencyMs}.",
                    message.CorrelationId,
                    recipient,
                    SmsFailureCategory.Timeout,
                    (int)stopwatch.ElapsedMilliseconds);
                return SmsSendResult.Failed(SmsFailureCategory.Timeout, latencyMs: (int)stopwatch.ElapsedMilliseconds);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "SMS provider could not be reached. Correlation {CorrelationId}, recipient {Recipient}, failure {Failure}.",
                    message.CorrelationId,
                    recipient,
                    SmsFailureCategory.Network);
                return SmsSendResult.Failed(SmsFailureCategory.Network, latencyMs: (int)stopwatch.ElapsedMilliseconds);
            }
        }

        private HttpRequestMessage BuildRequest(SmsOtpMessage message, string mobiles)
        {
            // The variable name (otp) must match the ##variable## of the approved MSG91 flow template, which has no other.
            var payload = new Dictionary<string, object>
            {
                ["template_id"] = _options.FlowId,
                ["recipients"] = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["mobiles"] = mobiles,
                        [Msg91Options.OtpVariableName] = message.Otp
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, FlowPath)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation(AuthKeyHeader, _options.AuthKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        private SmsSendResult Interpret(
            SmsOtpMessage message,
            string? recipient,
            HttpStatusCode statusCode,
            string body,
            int latencyMs)
        {
            var status = (int)statusCode;
            var reply = ProviderReply.Parse(body);
            var isSuccessStatus = status is >= 200 and < 300;

            if (isSuccessStatus && reply.IsSuccess)
            {
                var requestId = Truncate(reply.Message, MaxProviderRequestIdLength);
                _logger.LogInformation(
                    "SMS provider accepted OTP request. Correlation {CorrelationId}, recipient {Recipient}, httpStatus {HttpStatus}, providerRequestId {ProviderRequestId}, latencyMs {LatencyMs}.",
                    message.CorrelationId,
                    recipient,
                    status,
                    requestId,
                    latencyMs);
                return SmsSendResult.Success(status, requestId, latencyMs);
            }

            var failure = isSuccessStatus && !reply.IsError
                ? SmsFailureCategory.InvalidResponse
                : Classify(status, reply);

            var level = failure is SmsFailureCategory.Authentication or SmsFailureCategory.Template
                ? LogLevel.Error
                : LogLevel.Warning;
            _logger.Log(
                level,
                "SMS provider did not accept OTP request. Correlation {CorrelationId}, recipient {Recipient}, httpStatus {HttpStatus}, failure {Failure}, providerCode {ProviderCode}, providerMessage {ProviderMessage}, latencyMs {LatencyMs}.",
                message.CorrelationId,
                recipient,
                status,
                failure,
                AuditSanitizer.SanitizeCode(reply.Code, 16),
                AuditSanitizer.SanitizeText(Scrub(reply.Message, _options.AuthKey, message.Otp), MaxLoggedProviderMessageLength),
                latencyMs);

            return SmsSendResult.Failed(failure, status, latencyMs);
        }

        /// <summary>The provider's text is only ever logged after our own secrets are removed from it, in case it echoes them.</summary>
        private static string? Scrub(string? text, params string[] secrets)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            foreach (var secret in secrets)
            {
                if (secret.Length > 0)
                {
                    text = text.Replace(secret, AuditSanitizer.Redacted, StringComparison.Ordinal);
                }
            }

            return text;
        }

        private static SmsFailureCategory Classify(int status, ProviderReply reply)
        {
            var text = reply.Message?.ToLowerInvariant() ?? string.Empty;

            if (status is 401 or 403
                || reply.Code == "401"
                || text.Contains("authentication", StringComparison.Ordinal)
                || text.Contains("authkey", StringComparison.Ordinal)
                || text.Contains("auth key", StringComparison.Ordinal)
                || text.Contains("ip not whitelisted", StringComparison.Ordinal)
                || text.Contains("ip blocked", StringComparison.Ordinal))
            {
                return SmsFailureCategory.Authentication;
            }

            if (text.Contains("template", StringComparison.Ordinal) || text.Contains("flow", StringComparison.Ordinal))
            {
                return SmsFailureCategory.Template;
            }

            if (status == 429)
            {
                return SmsFailureCategory.ProviderRateLimited;
            }

            if (status is 408 or >= 500)
            {
                return SmsFailureCategory.ProviderError;
            }

            return SmsFailureCategory.Rejected;
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        /// <summary>MSG91 replies <c>{"type":"success","message":"&lt;request id&gt;"}</c> or <c>{"type":"error","message":"...","code":"..."}</c>.</summary>
        private readonly record struct ProviderReply(string? Type, string? Message, string? Code)
        {
            public bool IsSuccess => string.Equals(Type, "success", StringComparison.OrdinalIgnoreCase);

            public bool IsError => string.Equals(Type, "error", StringComparison.OrdinalIgnoreCase);

            public static ProviderReply Parse(string body)
            {
                if (string.IsNullOrWhiteSpace(body))
                {
                    return default;
                }

                try
                {
                    using var document = JsonDocument.Parse(body);
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return default;
                    }

                    return new ProviderReply(
                        ReadText(document.RootElement, "type"),
                        ReadText(document.RootElement, "message"),
                        ReadText(document.RootElement, "code"));
                }
                catch (JsonException)
                {
                    return default;
                }
            }

            private static string? ReadText(JsonElement element, string name)
            {
                if (!element.TryGetProperty(name, out var value))
                {
                    return null;
                }

                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    _ => null
                };
            }
        }
    }
}
