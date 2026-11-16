using Yakku.Application.Guests.DTOs;

namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestIdentityService
    {
        Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            GuestClientInfo? client = null,
            CancellationToken cancellationToken = default);
    }
}
