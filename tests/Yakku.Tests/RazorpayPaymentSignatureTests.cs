using Yakku.Application.Payments;

namespace Yakku.Tests;

public sealed class RazorpayPaymentSignatureTests
{
    [Fact]
    public void ValidSignature_MatchesHmacSha256()
    {
        const string orderId = "order_ABC";
        const string paymentId = "pay_XYZ";
        const string secret = "test_secret";
        var signature = RazorpayPaymentSignature.ComputeHexHmacSha256($"{orderId}|{paymentId}", secret);

        Assert.True(RazorpayPaymentSignature.IsValid(orderId, paymentId, signature, secret));
    }

    [Fact]
    public void InvalidSignature_IsRejected()
    {
        Assert.False(
            RazorpayPaymentSignature.IsValid(
                "order_ABC",
                "pay_XYZ",
                "deadbeef",
                "test_secret"));
    }

    [Fact]
    public void TamperedPaymentId_IsRejected()
    {
        const string secret = "test_secret";
        var signature = RazorpayPaymentSignature.ComputeHexHmacSha256("order_ABC|pay_XYZ", secret);
        Assert.False(RazorpayPaymentSignature.IsValid("order_ABC", "pay_OTHER", signature, secret));
    }
}
