using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Infrastructure.Configuration;

namespace Yakku.Infrastructure.Firebase
{
    public sealed class FirebaseNotificationProvider : IFirebaseNotificationProvider
    {
        private const int MaxTokensPerBatch = 500;
        private static readonly object InitLock = new();

        private readonly FirebaseMessaging _messaging;
        private readonly ILogger<FirebaseNotificationProvider> _logger;

        public FirebaseNotificationProvider(ILogger<FirebaseNotificationProvider> logger)
        {
            _logger = logger;
            EnsureFirebaseApp();
            _messaging = FirebaseMessaging.DefaultInstance;
        }

        public async Task<FirebaseTokenSendResult> SendAsync(
            string pushToken,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pushToken))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Push token is required.");
            }

            var message = BuildMessage(pushToken, payload);
            var fingerprint = TokenFingerprint(pushToken);

            try
            {
                var messageId = await _messaging.SendAsync(message, cancellationToken);
                _logger.LogInformation(
                    "FCM accepted message for token {TokenFingerprint}; messageId={MessageId}",
                    fingerprint,
                    messageId);

                return new FirebaseTokenSendResult
                {
                    TokenFingerprint = fingerprint,
                    Success = true,
                    MessageId = messageId
                };
            }
            catch (FirebaseMessagingException exception)
            {
                var invalid = IsInvalidToken(exception);
                _logger.LogWarning(
                    exception,
                    "FCM send failed for token {TokenFingerprint}; errorCode={ErrorCode}; invalidToken={InvalidToken}",
                    fingerprint,
                    exception.MessagingErrorCode?.ToString() ?? exception.ErrorCode.ToString(),
                    invalid);

                return new FirebaseTokenSendResult
                {
                    TokenFingerprint = fingerprint,
                    Success = false,
                    IsInvalidToken = invalid,
                    ErrorCode = exception.MessagingErrorCode?.ToString() ?? exception.ErrorCode.ToString(),
                    ErrorMessage = "Firebase messaging request failed."
                };
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected FCM send failure for token {TokenFingerprint}",
                    fingerprint);

                throw new AppException(
                    502,
                    ApiErrorCodes.InternalServerError,
                    "Failed to send push notification.");
            }
        }

        public async Task<IReadOnlyList<FirebaseTokenSendResult>> SendToManyAsync(
            IReadOnlyList<string> pushTokens,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (pushTokens.Count == 0)
            {
                return [];
            }

            var results = new List<FirebaseTokenSendResult>(pushTokens.Count);

            for (var offset = 0; offset < pushTokens.Count; offset += MaxTokensPerBatch)
            {
                var batch = pushTokens
                    .Skip(offset)
                    .Take(MaxTokensPerBatch)
                    .ToList();

                // FCM device registration tokens (not Firebase Installation IDs / FIDs).
#pragma warning disable CS0618
                var multicast = new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new Notification
                    {
                        Title = payload.Title,
                        Body = payload.Body
                    },
                    Data = NormalizeData(payload.Data)
                };
#pragma warning restore CS0618

                try
                {
                    var batchResponse = await _messaging.SendEachForMulticastAsync(
                        multicast,
                        cancellationToken);

                    _logger.LogInformation(
                        "FCM multicast completed for {TokenCount} tokens; success={SuccessCount}; failure={FailureCount}",
                        batch.Count,
                        batchResponse.SuccessCount,
                        batchResponse.FailureCount);

                    for (var index = 0; index < batch.Count; index++)
                    {
                        var token = batch[index];
                        var fingerprint = TokenFingerprint(token);
                        var response = batchResponse.Responses[index];

                        if (response.IsSuccess)
                        {
                            results.Add(new FirebaseTokenSendResult
                            {
                                TokenFingerprint = fingerprint,
                                Success = true,
                                MessageId = response.MessageId
                            });
                            continue;
                        }

                        var exception = response.Exception;
                        var invalid = exception is not null && IsInvalidToken(exception);
                        results.Add(new FirebaseTokenSendResult
                        {
                            TokenFingerprint = fingerprint,
                            Success = false,
                            IsInvalidToken = invalid,
                            ErrorCode = exception?.MessagingErrorCode?.ToString()
                                         ?? exception?.ErrorCode.ToString()
                                         ?? "UNKNOWN",
                            ErrorMessage = "Firebase messaging request failed."
                        });
                    }
                }
                catch (Exception exception) when (exception is not AppException)
                {
                    _logger.LogError(
                        exception,
                        "Unexpected FCM multicast failure for {TokenCount} tokens",
                        batch.Count);

                    throw new AppException(
                        502,
                        ApiErrorCodes.InternalServerError,
                        "Failed to send push notification.");
                }
            }

            return results;
        }

        private static Message BuildMessage(string pushToken, NotificationPayload payload)
        {
            // FCM device registration tokens (not Firebase Installation IDs / FIDs).
#pragma warning disable CS0618
            return new Message
            {
                Token = pushToken,
                Notification = new Notification
                {
                    Title = payload.Title,
                    Body = payload.Body
                },
                Data = NormalizeData(payload.Data)
            };
#pragma warning restore CS0618
        }

        private static Dictionary<string, string>? NormalizeData(Dictionary<string, string>? data)
        {
            if (data is null || data.Count == 0)
            {
                return null;
            }

            return data.ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? string.Empty);
        }

        private static bool IsInvalidToken(FirebaseMessagingException exception)
        {
            // InvalidArgument can also mean a bad payload; only treat clear token failures as invalid.
            return exception.MessagingErrorCode is MessagingErrorCode.Unregistered
                or MessagingErrorCode.SenderIdMismatch;
        }

        private static string TokenFingerprint(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return "empty";
            }

            if (token.Length <= 10)
            {
                return $"len:{token.Length}";
            }

            return $"{token[..4]}…{token[^4..]}(len:{token.Length})";
        }

        private static void EnsureFirebaseApp()
        {
            if (FirebaseApp.DefaultInstance is not null)
            {
                return;
            }

            lock (InitLock)
            {
                if (FirebaseApp.DefaultInstance is not null)
                {
                    return;
                }

                var credential = LoadCredential();
                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential
                });
            }
        }

        private static GoogleCredential LoadCredential()
        {
            EnvFile.Load();

            var type = GetEnv("FIREBASE_TYPE") ?? "service_account";
            var projectId = GetEnv("FIREBASE_PROJECT_ID");
            var privateKeyId = GetEnv("FIREBASE_PRIVATE_KEY_ID");
            var privateKey = GetEnv("FIREBASE_PRIVATE_KEY");
            var clientEmail = GetEnv("FIREBASE_CLIENT_EMAIL");
            var clientId = GetEnv("FIREBASE_CLIENT_ID");
            var authUri = GetEnv("FIREBASE_AUTH_URI") ?? "https://accounts.google.com/o/oauth2/auth";
            var tokenUri = GetEnv("FIREBASE_TOKEN_URI") ?? "https://oauth2.googleapis.com/token";
            var authProviderCertUrl = GetEnv("FIREBASE_AUTH_PROVIDER_X509_CERT_URL")
                ?? "https://www.googleapis.com/oauth2/v1/certs";
            var clientCertUrl = GetEnv("FIREBASE_CLIENT_X509_CERT_URL");
            var universeDomain = GetEnv("FIREBASE_UNIVERSE_DOMAIN") ?? "googleapis.com";

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(projectId)) missing.Add("FIREBASE_PROJECT_ID");
            if (string.IsNullOrWhiteSpace(privateKeyId)) missing.Add("FIREBASE_PRIVATE_KEY_ID");
            if (string.IsNullOrWhiteSpace(privateKey)) missing.Add("FIREBASE_PRIVATE_KEY");
            if (string.IsNullOrWhiteSpace(clientEmail)) missing.Add("FIREBASE_CLIENT_EMAIL");
            if (string.IsNullOrWhiteSpace(clientId)) missing.Add("FIREBASE_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(clientCertUrl)) missing.Add("FIREBASE_CLIENT_X509_CERT_URL");

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Firebase credentials are incomplete. Set these keys in your .env file: "
                    + string.Join(", ", missing));
            }

            // .env stores PEM newlines as literal \n sequences on one line.
            privateKey = privateKey!.Replace("\\n", "\n", StringComparison.Ordinal);

            var credentialJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["type"] = type,
                ["project_id"] = projectId!,
                ["private_key_id"] = privateKeyId!,
                ["private_key"] = privateKey,
                ["client_email"] = clientEmail!,
                ["client_id"] = clientId!,
                ["auth_uri"] = authUri,
                ["token_uri"] = tokenUri,
                ["auth_provider_x509_cert_url"] = authProviderCertUrl,
                ["client_x509_cert_url"] = clientCertUrl!,
                ["universe_domain"] = universeDomain
            });

            return CredentialFactory.FromJson<ServiceAccountCredential>(credentialJson).ToGoogleCredential();
        }

        private static string? GetEnv(string key)
        {
            return Environment.GetEnvironmentVariable(key)?.Trim().Trim('"').Trim('\'');
        }
    }
}
