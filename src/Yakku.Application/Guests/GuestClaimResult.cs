namespace Yakku.Application.Guests
{
    public enum GuestClaimResult
    {
        /// <summary>No guest token was supplied.</summary>
        NoToken = 0,

        /// <summary>The token matches no guest (unknown, already claimed or already purged).</summary>
        NotFound = 1,

        /// <summary>The guest exists but has expired, so its data is never transferred.</summary>
        Expired = 2,

        /// <summary>Guest data was moved to the user and the guest was deleted.</summary>
        Claimed = 3,

        /// <summary>The claim threw and was rolled back. The guest and its data are unchanged.</summary>
        Failed = 4
    }
}
