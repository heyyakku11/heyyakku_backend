using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.Payments
{
    /// <summary>
    /// Razorpay webhook signature: HMAC-SHA256 of the <b>raw</b> request body with the webhook secret.
    /// Must not run on re-serialized JSON.
    /// </summary>
    public static class RazorpayWebhookSignature
    {
        public static bool IsValid(ReadOnlySpan<byte> rawBody, string signature, string webhookSecret)
        {
            if (rawBody.IsEmpty || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(webhookSecret))
            {
                return false;
            }

            var expected = ComputeHexHmacSha256(rawBody, webhookSecret);
            var provided = signature.Trim().ToLowerInvariant();
            if (expected.Length != provided.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided));
        }

        public static bool IsValid(string rawBody, string signature, string webhookSecret)
        {
            return IsValid(Encoding.UTF8.GetBytes(rawBody ?? string.Empty), signature, webhookSecret);
        }

        public static string ComputeHexHmacSha256(ReadOnlySpan<byte> rawBody, string webhookSecret)
        {
            var key = Encoding.UTF8.GetBytes(webhookSecret);
            var hash = HMACSHA256.HashData(key, rawBody);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
