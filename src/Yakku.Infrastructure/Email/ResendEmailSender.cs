using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.Infrastructure.Email
{
    public class ResendEmailSender : IEmailSender
    {
        private const string ExpiryTime = "5 minutes";

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

        public async Task SendOtpAsync(string email, string otp, CancellationToken cancellationToken = default)
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
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(exception, "Failed to reach Resend while sending OTP email.");
                throw new AppException(
                    502,
                    ApiErrorCodes.InternalServerError,
                    "Failed to send OTP email. Please try again.");
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OTP email dispatched via Resend.");
                return;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Resend rejected OTP email with status {StatusCode}: {Error}",
                (int)response.StatusCode,
                errorBody);

            throw new AppException(
                502,
                ApiErrorCodes.InternalServerError,
                "Failed to send OTP email. Please try again.");
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
