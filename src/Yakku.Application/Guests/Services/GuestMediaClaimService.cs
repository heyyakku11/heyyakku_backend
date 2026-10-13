using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls.Interfaces;

namespace Yakku.Application.Guests.Services
{
    public sealed class GuestMediaClaimService : IGuestMediaClaimService
    {
        private readonly IGuestRepository _guests;
        private readonly IGuestTokenHasher _tokenHasher;
        private readonly IMediaUploadSessionRepository _sessions;

        public GuestMediaClaimService(
            IGuestRepository guests,
            IGuestTokenHasher tokenHasher,
            IMediaUploadSessionRepository sessions)
        {
            _guests = guests;
            _tokenHasher = tokenHasher;
            _sessions = sessions;
        }

        public async Task ClaimAndDeleteGuestAsync(
            string? rawGuestToken,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawGuestToken))
            {
                return;
            }

            var guest = await _guests.GetByTokenHashAsync(
                _tokenHasher.Hash(rawGuestToken.Trim()),
                cancellationToken);
            if (guest is null)
            {
                return;
            }

            await _sessions.ClaimGuestSessionsAsync(guest.Id, userId, cancellationToken);
            _guests.Delete(guest);
            await _guests.SaveChangesAsync(cancellationToken);
        }
    }
}
