using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Application.Users.Services
{
    public class AnonymousNameAllocator : IAnonymousNameAllocator
    {
        private readonly IDisplayNameGenerator _generator;
        private readonly IUserRepository _users;
        private readonly IAnonymousNameReservations _reservations;

        public AnonymousNameAllocator(
            IDisplayNameGenerator generator,
            IUserRepository users,
            IAnonymousNameReservations reservations)
        {
            _generator = generator;
            _users = users;
            _reservations = reservations;
        }

        public async Task<string> ReserveAsync(TimeSpan holdFor, CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < OperationalSettings.AnonymousNames.MaxAttempts; attempt++)
            {
                var candidate = _generator.Generate();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (await _users.AnonymousNameExistsAsync(candidate, cancellationToken))
                {
                    continue;
                }

                if (await _reservations.TryReserveAsync(candidate, holdFor, cancellationToken))
                {
                    return candidate;
                }
            }

            throw new AppException(
                503,
                ApiErrorCodes.AnonymousNameUnavailable,
                "Could not find a free anonymous name. Please try again.",
                "anonymousName");
        }

        public Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
            => _reservations.RefreshAsync(name, holdFor, cancellationToken);

        public Task ReleaseAsync(string name, CancellationToken cancellationToken = default)
            => _reservations.ReleaseAsync(name, cancellationToken);
    }
}
