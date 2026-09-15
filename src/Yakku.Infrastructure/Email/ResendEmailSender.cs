using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;

namespace Yakku.Infrastructure.Email
{
    public class ResendEmailSender : IEmailSender
    {
        private const string ExpiryTime = "5 minutes";
        private const int MaxErrorBodyLength = 1000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly string _templateId;
        private readonly ILogger<ResendEmailSender> _logger;

        public ResendEmailSender(
            HttpClient httpClient,
            string fromEmail,
            string fromName,
            string templateId,
            ILogger<ResendEmailSender> logger)
        {
            _httpClient = httpClient;
            _fromEmail = fromEmail;
            _fromName = fromName;
            _templateId = templateId;
            _logger = logger;
        }

        public static ResendEmailSender Create(
            string apiKey,
            string fromEmail,
            string fromName,
            string templateId,
            ILogger<ResendEmailSender> logger)
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.resend.com/")
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            return new ResendEmailSender(httpClient, fromEmail, fromName, templateId, logger);
        }

        public async Task<EmailSendResult> SendOtpAsync(
            string email,
            string otp,
            CancellationToken cancellationToken = default)
        {
            var payload = new ResendEmailRequest
            {
                From = $"{_fromName} <{_fromEmail}>",
                To = [email],
                Subject = "Your Yakku verification code",
                Template = new ResendTemplate
                {
                    Id = _templateId,
                    Variables = new Dictionary<string, string>
                    {
                        ["otp"] = otp,
                        ["expiry_time"] = ExpiryTime
                    }
                }
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync("emails", payload, JsonOptions, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(exception, "Failed to reach Resend while sending OTP email.");
                return EmailSendResult.TransientFailure("NetworkError", "Failed to reach email provider.");
            }

            if (response.IsSuccessStatusCode)
            {
                var providerMessageId = await TryReadProviderMessageIdAsync(response, cancellationToken);
                _logger.LogInformation("OTP email dispatched via Resend.");
                return EmailSendResult.Success(providerMessageId);
            }

            var errorBody = Truncate(await response.Content.ReadAsStringAsync(cancellationToken));
            var statusCode = (int)response.StatusCode;
            _logger.LogError(
                "Resend rejected OTP email with status {StatusCode}: {Error}",
                statusCode,
                errorBody);

            var errorCode = $"Http{statusCode}";
            if (IsTransientStatus(response.StatusCode))
            {
                return EmailSendResult.TransientFailure(errorCode, errorBody);
            }

            return EmailSendResult.PermanentFailure(errorCode, errorBody);
        }

        private static bool IsTransientStatus(HttpStatusCode statusCode)
        {
            var code = (int)statusCode;
            if (code is 408 or 429)
            {
                return true;
            }

            return code >= 500;
        }

        private static async Task<string?> TryReadProviderMessageIdAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("id", out var idProperty)
                    && idProperty.ValueKind == JsonValueKind.String)
                {
                    return idProperty.GetString();
                }
            }
            catch (JsonException)
            {
                // Provider accepted the email; missing id is non-fatal.
            }

            return null;
        }

        private static string? Truncate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= MaxErrorBodyLength
                ? trimmed
                : trimmed[..MaxErrorBodyLength];
        }

        private sealed class ResendEmailRequest
        {
            public string From { get; init; } = string.Empty;
            public string[] To { get; init; } = [];
            public string Subject { get; init; } = string.Empty;
            public ResendTemplate Template { get; init; } = null!;
        }

        private sealed class ResendTemplate
        {
            public string Id { get; init; } = string.Empty;
            public Dictionary<string, string> Variables { get; init; } = [];
        }
    }
}
