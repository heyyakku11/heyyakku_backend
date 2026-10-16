namespace Yakku.Application.Devices
{
    /// <summary>
    /// Who is calling, as established by the API layer from the validated JWT or guest credential.
    /// Ownership is never taken from the request body.
    /// </summary>
    /// <param name="UserId">The authenticated user, if any.</param>
    /// <param name="GuestId">The validated guest, when the caller is not a signed-in user.</param>
    /// <param name="SessionId">The caller's login session (users only).</param>
    /// <param name="PresentedGuestToken">
    /// A raw guest token the signed-in user also sent. It is only ever used to prove they control a
    /// guest-owned installation they are taking over, and is never stored or logged.
    /// </param>
    public sealed record DeviceCaller(
        Guid? UserId,
        Guid? GuestId,
        Guid? SessionId,
        string? PresentedGuestToken)
    {
        public bool IsUser => UserId is not null;

        public static DeviceCaller ForUser(Guid userId, Guid sessionId, string? presentedGuestToken = null)
            => new(userId, null, sessionId, presentedGuestToken);

        public static DeviceCaller ForGuest(Guid guestId)
            => new(null, guestId, null, null);
    }
}
