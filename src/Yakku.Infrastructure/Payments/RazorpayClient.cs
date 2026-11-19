using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Yakku.Application.Configuration;
using Yakku.Application.Payments.Interfaces;

namespace Yakku.Infrastructure.Payments
{
    /// <summary>
    /// Typed Razorpay HTTP client. Basic auth with Key ID + Key Secret. Secrets never appear in logs.
    /// Order creation is not retried: callers must use an idempotency key at the application layer.
    /// </summary>
    public sealed class RazorpayClient : IRazorpayClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly RazorpayOptions _options;
        private readonly ILogger<RazorpayClient> _logger;

        public RazorpayClient(HttpClient httpClient, RazorpayOptions options, ILogger<RazorpayClient> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;
        }

        public bool IsEnabled => _options.Enabled;

        public async Task<RazorpayOrderResult> CreateOrderAsync(
            RazorpayCreateOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return new RazorpayOrderResult(
                    false, null, null, null, null, "disabled", "Razorpay is disabled.");
            }

            var payload = new RazorpayCreateOrderBody
            {
                Amount = request.AmountInPaise,
                Currency = request.Currency,
                Receipt = request.Receipt,
                PaymentCapture = request.PaymentCapture ? 1 : 0,
                Notes = string.IsNullOrWhiteSpace(request.NotesCorrelationId)
                    ? null
                    : new Dictionary<string, string> { ["correlation_id"] = request.NotesCorrelationId }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "orders")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            httpRequest.Headers.Authorization = BuildBasicAuth();

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    var created = JsonSerializer.Deserialize<RazorpayOrderResponseBody>(body, JsonOptions);
                    if (created is null || string.IsNullOrWhiteSpace(created.Id))
                    {
                        _logger.LogWarning(
                            "Razorpay order response missing id. Status {StatusCode}, latencyMs {LatencyMs}.",
                            (int)response.StatusCode,
                            (int)stopwatch.ElapsedMilliseconds);
                        return new RazorpayOrderResult(
                            false, null, null, null, null, "invalid_response", "Missing order id.");
                    }

                    _logger.LogInformation(
                        "Razorpay order created. Receipt {Receipt}, latencyMs {LatencyMs}.",
                        request.Receipt,
                        (int)stopwatch.ElapsedMilliseconds);

                    return new RazorpayOrderResult(
                        true,
                        created.Id,
                        created.Amount,
                        created.Currency,
                        created.Status,
                        null,
                        null);
                }

                var error = TryReadError(body);
                _logger.LogWarning(
                    "Razorpay order creation rejected. Status {StatusCode}, ErrorCode {ErrorCode}, latencyMs {LatencyMs}.",
                    (int)response.StatusCode,
                    error.Code ?? response.StatusCode.ToString(),
                    (int)stopwatch.ElapsedMilliseconds);

                return new RazorpayOrderResult(
                    false,
                    null,
                    null,
                    null,
                    null,
                    error.Code ?? ((int)response.StatusCode).ToString(),
                    error.Description ?? "Razorpay rejected the order.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Razorpay order creation timed out. Receipt {Receipt}, latencyMs {LatencyMs}.",
                    request.Receipt,
                    (int)stopwatch.ElapsedMilliseconds);
                return new RazorpayOrderResult(
                    false, null, null, null, null, "timeout", "Razorpay did not answer in time.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Razorpay order creation network failure. Receipt {Receipt}.",
                    request.Receipt);
                return new RazorpayOrderResult(
                    false, null, null, null, null, "network", "Razorpay could not be reached.");
            }
        }

        public async Task<RazorpayPaymentResult> GetPaymentAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return FailedPayment("disabled", "Razorpay is disabled.");
            }

            if (string.IsNullOrWhiteSpace(razorpayPaymentId))
            {
                return FailedPayment("invalid_payment_id", "Payment id is required.");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"payments/{razorpayPaymentId.Trim()}");
            httpRequest.Headers.Authorization = BuildBasicAuth();

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    var error = TryReadError(body);
                    _logger.LogWarning(
                        "Razorpay payment fetch rejected. Status {StatusCode}, ErrorCode {ErrorCode}, latencyMs {LatencyMs}.",
                        (int)response.StatusCode,
                        error.Code ?? response.StatusCode.ToString(),
                        (int)stopwatch.ElapsedMilliseconds);
                    return FailedPayment(
                        error.Code ?? ((int)response.StatusCode).ToString(),
                        error.Description ?? "Razorpay rejected the payment fetch.");
                }

                var payment = JsonSerializer.Deserialize<RazorpayPaymentResponseBody>(body, JsonOptions);
                if (payment is null || string.IsNullOrWhiteSpace(payment.Id))
                {
                    return FailedPayment("invalid_response", "Missing payment id.");
                }

                _logger.LogInformation(
                    "Razorpay payment fetched. Status {PaymentStatus}, latencyMs {LatencyMs}.",
                    payment.Status,
                    (int)stopwatch.ElapsedMilliseconds);

                return new RazorpayPaymentResult(
                    true,
                    payment.Id,
                    payment.OrderId,
                    payment.Amount,
                    payment.Currency,
                    payment.Status,
                    payment.Captured || string.Equals(payment.Status, "captured", StringComparison.OrdinalIgnoreCase),
                    payment.Method,
                    payment.ErrorCode,
                    payment.ErrorDescription);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Razorpay payment fetch timed out. PaymentId {PaymentId}, latencyMs {LatencyMs}.",
                    razorpayPaymentId,
                    (int)stopwatch.ElapsedMilliseconds);
                return FailedPayment("timeout", "Razorpay did not answer in time.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Razorpay payment fetch network failure. PaymentId {PaymentId}.",
                    razorpayPaymentId);
                return FailedPayment("network", "Razorpay could not be reached.");
            }
        }

        public async Task<IReadOnlyList<RazorpayPaymentResult>> GetOrderPaymentsAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return [];
            }

            if (string.IsNullOrWhiteSpace(razorpayOrderId))
            {
                return [];
            }

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"orders/{razorpayOrderId.Trim()}/payments");
            httpRequest.Headers.Authorization = BuildBasicAuth();

            try
            {
                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var error = TryReadError(body);
                    _logger.LogWarning(
                        "Razorpay order payments fetch rejected. Status {StatusCode}, ErrorCode {ErrorCode}.",
                        (int)response.StatusCode,
                        error.Code ?? response.StatusCode.ToString());
                    return [];
                }

                var collection = JsonSerializer.Deserialize<RazorpayPaymentCollectionBody>(body, JsonOptions);
                if (collection?.Items is null || collection.Items.Count == 0)
                {
                    return [];
                }

                return collection.Items
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => new RazorpayPaymentResult(
                        true,
                        item.Id,
                        item.OrderId ?? razorpayOrderId,
                        item.Amount,
                        item.Currency,
                        item.Status,
                        item.Captured
                        || string.Equals(item.Status, "captured", StringComparison.OrdinalIgnoreCase),
                        item.Method,
                        item.ErrorCode,
                        item.ErrorDescription))
                    .ToList();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
            {
                _logger.LogWarning(
                    exception,
                    "Razorpay order payments fetch failed for order {OrderId}.",
                    razorpayOrderId);
                return [];
            }
        }

        public async Task<RazorpayRefundResult> CreateRefundAsync(
            RazorpayCreateRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return new RazorpayRefundResult(false, null, null, null, "disabled", "Razorpay is disabled.");
            }

            if (string.IsNullOrWhiteSpace(request.RazorpayPaymentId) || request.AmountInPaise <= 0)
            {
                return new RazorpayRefundResult(
                    false, null, null, null, "invalid_request", "Payment id and amount are required.");
            }

            var payload = new RazorpayCreateRefundBody
            {
                Amount = request.AmountInPaise,
                Notes = string.IsNullOrWhiteSpace(request.NotesReason)
                    ? null
                    : new Dictionary<string, string> { ["reason"] = request.NotesReason.Trim() }
            };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"payments/{request.RazorpayPaymentId.Trim()}/refund")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            httpRequest.Headers.Authorization = BuildBasicAuth();

            try
            {
                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var error = TryReadError(body);
                    _logger.LogWarning(
                        "Razorpay refund rejected. Status {StatusCode}, ErrorCode {ErrorCode}.",
                        (int)response.StatusCode,
                        error.Code ?? response.StatusCode.ToString());
                    return new RazorpayRefundResult(
                        false,
                        null,
                        null,
                        null,
                        error.Code ?? ((int)response.StatusCode).ToString(),
                        error.Description ?? "Razorpay rejected the refund.");
                }

                var refund = JsonSerializer.Deserialize<RazorpayRefundResponseBody>(body, JsonOptions);
                if (refund is null || string.IsNullOrWhiteSpace(refund.Id))
                {
                    return new RazorpayRefundResult(
                        false, null, null, null, "invalid_response", "Missing refund id.");
                }

                return new RazorpayRefundResult(
                    true, refund.Id, refund.Status, refund.Amount, null, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return new RazorpayRefundResult(
                    false, null, null, null, "timeout", "Razorpay did not answer in time.");
            }
            catch (HttpRequestException)
            {
                return new RazorpayRefundResult(
                    false, null, null, null, "network", "Razorpay could not be reached.");
            }
        }

        private static RazorpayPaymentResult FailedPayment(string code, string message)
        {
            return new RazorpayPaymentResult(
                false, null, null, null, null, null, false, null, code, message);
        }

        private AuthenticationHeaderValue BuildBasicAuth()
        {
            var raw = $"{_options.KeyId}:{_options.KeySecret}";
            var bytes = Encoding.UTF8.GetBytes(raw);
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }

        private static (string? Code, string? Description) TryReadError(string body)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    var code = error.TryGetProperty("code", out var codeNode) ? codeNode.GetString() : null;
                    var description = error.TryGetProperty("description", out var descNode)
                        ? descNode.GetString()
                        : null;
                    return (code, description);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed error bodies.
            }

            return (null, null);
        }

        private sealed class RazorpayCreateOrderBody
        {
            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; } = "INR";

            [JsonPropertyName("receipt")]
            public string Receipt { get; set; } = string.Empty;

            [JsonPropertyName("payment_capture")]
            public int PaymentCapture { get; set; }

            [JsonPropertyName("notes")]
            public Dictionary<string, string>? Notes { get; set; }
        }

        private sealed class RazorpayOrderResponseBody
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("currency")]
            public string? Currency { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }
        }

        private sealed class RazorpayPaymentResponseBody
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("order_id")]
            public string? OrderId { get; set; }

            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("currency")]
            public string? Currency { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("captured")]
            public bool Captured { get; set; }

            [JsonPropertyName("method")]
            public string? Method { get; set; }

            [JsonPropertyName("error_code")]
            public string? ErrorCode { get; set; }

            [JsonPropertyName("error_description")]
            public string? ErrorDescription { get; set; }
        }

        private sealed class RazorpayPaymentCollectionBody
        {
            [JsonPropertyName("items")]
            public List<RazorpayPaymentResponseBody>? Items { get; set; }
        }

        private sealed class RazorpayCreateRefundBody
        {
            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("notes")]
            public Dictionary<string, string>? Notes { get; set; }
        }

        private sealed class RazorpayRefundResponseBody
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }
        }
    }

    /// <summary>Used when Razorpay is disabled so DI still resolves <see cref="IRazorpayClient"/>.</summary>
    public sealed class DisabledRazorpayClient : IRazorpayClient
    {
        public bool IsEnabled => false;

        public Task<RazorpayOrderResult> CreateOrderAsync(
            RazorpayCreateOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayOrderResult(
                    false, null, null, null, null, "disabled", "Razorpay is disabled."));
        }

        public Task<RazorpayPaymentResult> GetPaymentAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayPaymentResult(
                    false, null, null, null, null, null, false, null, "disabled", "Razorpay is disabled."));
        }

        public Task<IReadOnlyList<RazorpayPaymentResult>> GetOrderPaymentsAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RazorpayPaymentResult>>([]);
        }

        public Task<RazorpayRefundResult> CreateRefundAsync(
            RazorpayCreateRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayRefundResult(false, null, null, null, "disabled", "Razorpay is disabled."));
        }
    }
}
