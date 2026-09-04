using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Application.Guests.Services
{
    public class GuestIdentityService : IGuestIdentityService
    {
        private readonly IGuestRepository _guestRepository;
        private readonly IGuestTokenGenerator _tokenGenerator;
        private readonly IGuestTokenHasher _tokenHasher;

        public GuestIdentityService(
            IGuestRepository guestRepository,
            IGuestTokenGenerator tokenGenerator,
            IGuestTokenHasher tokenHasher)
        {
            _guestRepository = guestRepository;
            _tokenGenerator = tokenGenerator;
            _tokenHasher = tokenHasher;
        }

        public async Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                var tokenHash = _tokenHasher.Hash(rawToken);
                var existing = await _guestRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
                if (existing is not null)
                {
                    existing.Touch();
                    await _guestRepository.SaveChangesAsync(cancellationToken);
                    return new GuestEstablishResult { GuestId = existing.Id };
                }
            }

            var token = _tokenGenerator.Generate();
            var guest = new Guest(_tokenHasher.Hash(token));
            await _guestRepository.AddAsync(guest, cancellationToken);
            await _guestRepository.SaveChangesAsync(cancellationToken);

            return new GuestEstablishResult
            {
                GuestId = guest.Id,
                RawTokenToSet = token
            };
        }
    }
}
