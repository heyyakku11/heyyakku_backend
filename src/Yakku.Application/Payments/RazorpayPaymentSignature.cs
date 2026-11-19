using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.Payments
{
    /// <summary>
    /// Razorpay Checkout callback signature: HMAC-SHA256 of <c>orderId|paymentId</c> with the key secret.
    /// </summary>
    public static class RazorpayPaymentSignature
    {
        public static bool IsValid(string razorpayOrderId, string razorpayPaymentId, string signature, string keySecret)
        {
            if (string.IsNullOrWhiteSpace(razorpayOrderId)
                || string.IsNullOrWhiteSpace(razorpayPaymentId)
                || string.IsNullOrWhiteSpace(signature)
                || string.IsNullOrWhiteSpace(keySecret))
            {
                return false;
            }

            var payload = $"{razorpayOrderId.Trim()}|{razorpayPaymentId.Trim()}";
            var expected = ComputeHexHmacSha256(payload, keySecret);
            var provided = signature.Trim().ToLowerInvariant();
            if (expected.Length != provided.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided));
        }

        public static string ComputeHexHmacSha256(string payload, string keySecret)
        {
            var key = Encoding.UTF8.GetBytes(keySecret);
            var data = Encoding.UTF8.GetBytes(payload);
            var hash = HMACSHA256.HashData(key, data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
