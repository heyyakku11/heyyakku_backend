namespace Yakku.Application.Guests.DTOs
{
    public class GuestEstablishResult
    {
        public Guid GuestId { get; init; }
        public string? RawTokenToSet { get; init; }
    }
}
