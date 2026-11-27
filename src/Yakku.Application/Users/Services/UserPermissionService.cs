using FluentValidation;
using Yakku.Application.Caching;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Application.Users.Services
{
    public class UserPermissionService : IUserPermissionService
    {
        private readonly IUserPermissionRepository _repository;
        private readonly IValidator<UpdateUserPermissionsRequest> _validator;
        private readonly TimeProvider _time;
        private readonly IResponseCache _cache;

        public UserPermissionService(
            IUserPermissionRepository repository,
            IValidator<UpdateUserPermissionsRequest> validator,
            TimeProvider time,
            IResponseCache? cache = null)
        {
            _repository = repository;
            _validator = validator;
            _time = time;
            _cache = cache ?? new NullResponseCache();
        }

        public async Task<UserPermissionsResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _repository.GetAsync(userId, cancellationToken) ?? throw NotFound();
        }

        public async Task<UserPermissionsResponse> UpdateAsync(
            Guid userId,
            UpdateUserPermissionsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var updated = await _repository.UpdateAsync(
                userId, request, _time.GetUtcNow().UtcDateTime, cancellationToken)
                ?? throw NotFound();

            // GET /users carries allowAskBack, so its cached copy is dropped.
            await _cache.RemoveManyAsync(ResponseCacheKeys.AfterUserProfileChange(userId), cancellationToken);
            return updated;
        }

        private static AppException NotFound()
            => new(404, ApiErrorCodes.NotFound, "User permissions not found.");
    }
}
