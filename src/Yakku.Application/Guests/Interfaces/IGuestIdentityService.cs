using Yakku.Application.Guests.DTOs;

namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestIdentityService
    {
        Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            string? idempotencyKey = null,
            GuestClientInfo? client = null,
            CancellationToken cancellationToken = default);
    }
}
