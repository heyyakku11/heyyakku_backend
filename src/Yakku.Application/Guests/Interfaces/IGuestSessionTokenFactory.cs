namespace Yakku.Application.Guests.Interfaces
{
    /// <summary>Derives a recoverable guest session token from an idempotency key.</summary>
    public interface IGuestSessionTokenFactory
    {
        string FromIdempotencyKey(string idempotencyKey);
    }
}
