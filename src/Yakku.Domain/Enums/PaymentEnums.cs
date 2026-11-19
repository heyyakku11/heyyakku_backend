namespace Yakku.Domain.Enums
{
    /// <summary>What a payment order is buying.</summary>
    public enum PaymentPurchaseType
    {
        /// <summary>A time-limited user plan (yakku_lite or yakku_love).</summary>
        UserPlan = 0,

        /// <summary>Permanent Ask Back unlock for one poll or check (<c>this_card</c>).</summary>
        CardUnlock = 1
    }

    /// <summary>Lifecycle of an internal payment order. A failed attempt does not necessarily fail the order.</summary>
    public enum PaymentOrderStatus
    {
        Created = 0,
        Pending = 1,
        Paid = 2,
        Failed = 3,
        Cancelled = 4,
        Expired = 5
    }

    /// <summary>Lifecycle of one Razorpay payment attempt against an internal order.</summary>
    public enum PaymentAttemptStatus
    {
        Created = 0,
        Authorized = 1,
        Captured = 2,
        Failed = 3
    }

    /// <summary>Lifecycle of a paid plan entitlement period. Free is never stored as an entitlement row.</summary>
    public enum UserPlanEntitlementStatus
    {
        Active = 0,
        Expired = 1,
        Superseded = 2,
        Revoked = 3
    }

    /// <summary>Durable webhook inbox processing state.</summary>
    public enum WebhookProcessingStatus
    {
        Received = 0,
        Processing = 1,
        Processed = 2,
        Failed = 3
    }

    /// <summary>Outcome recorded on a payment audit log row.</summary>
    public enum PaymentLogStatus
    {
        Information = 0,
        Success = 1,
        Warning = 2,
        Failure = 3
    }

    /// <summary>How Razorpay should capture authorized payments.</summary>
    public enum PaymentCaptureMode
    {
        Automatic = 0,
        Manual = 1
    }

    /// <summary>Admin-initiated refund lifecycle. Access is never revoked solely by this status.</summary>
    public enum PaymentRefundStatus
    {
        Requested = 0,
        Processed = 1,
        Failed = 2
    }
}
