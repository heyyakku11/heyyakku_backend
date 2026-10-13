namespace Yakku.Application.Guests.DTOs
{
    public class GuestResponse
    {
        public Guid GuestId { get; init; }
        public DateTime ExpiresAt { get; init; }

        /// <summary>
        /// Present only when a new guest token is issued. Store and send as X-Guest-Token on media APIs.
        /// </summary>
        public string? GuestToken { get; init; }
    }
}
