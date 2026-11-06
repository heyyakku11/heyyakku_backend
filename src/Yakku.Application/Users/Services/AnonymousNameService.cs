using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Application.Users.Services
{
    public class AnonymousNameService : IAnonymousNameService
    {
        private readonly IAnonymousNameAllocator _allocator;
        private readonly IAnonymousNameRepository _repository;

        public AnonymousNameService(IAnonymousNameAllocator allocator, IAnonymousNameRepository repository)
        {
            _allocator = allocator;
            _repository = repository;
        }

        public async Task<AnonymousNameResponse> RegenerateAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < OperationalSettings.AnonymousNames.MaxAttempts; attempt++)
            {
                // Free in the database and in Redis, and held briefly so a concurrent request cannot take it.
                var candidate = await _allocator.ReserveAsync(OperationalSettings.AnonymousNames.RegenerationHold, cancellationToken);
                try
                {
                    var result = await _repository.TryUpdateAsync(userId, candidate, cancellationToken);
                    if (result == AnonymousNameUpdateResult.Updated)
                    {
                        return new AnonymousNameResponse { AnonymousName = candidate };
                    }

                    if (result == AnonymousNameUpdateResult.ProfileNotFound)
                    {
                        throw new AppException(404, ApiErrorCodes.NotFound, "User profile not found.");
                    }

                    // Conflict: another writer took the name first. Loop with a fresh candidate.
                }
                finally
                {
                    // The hold has done its job: the database owns the name now, or it is free again.
                    await _allocator.ReleaseAsync(candidate, CancellationToken.None);
                }
            }

            throw new AppException(
                503,
                ApiErrorCodes.AnonymousNameUnavailable,
                "Could not find a free anonymous name. Please try again.",
                "anonymousName");
        }
    }
}
