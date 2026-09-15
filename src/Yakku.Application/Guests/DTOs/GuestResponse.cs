namespace Yakku.Application.Guests.DTOs
{
    public class GuestResponse
    {
        public Guid GuestId { get; init; }
        public DateTime ExpiresAt { get; init; }
    }
}
