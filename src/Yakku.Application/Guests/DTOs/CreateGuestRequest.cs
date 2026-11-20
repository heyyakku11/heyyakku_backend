namespace Yakku.Application.Guests.DTOs
{
    /// <summary>
    /// Creates or reuses a guest identity. Retries with the same <see cref="IdempotencyKey"/> return the same guest.
    /// Treat the key as sensitive: the session token is derived from it.
    /// </summary>
    public class CreateGuestRequest
    {
        /// <summary>Client-generated unique key; required when minting a new guest (no valid token yet).</summary>
        public string? IdempotencyKey { get; set; }
    }
}
