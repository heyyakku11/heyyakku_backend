using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Application.Users.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly IUserSettingsRepository _repository;
        private readonly IValidator<UpdateUserSettingsRequest> _validator;

        public UserSettingsService(
            IUserSettingsRepository repository,
            IValidator<UpdateUserSettingsRequest> validator)
        {
            _repository = repository;
            _validator = validator;
        }

        public async Task<UserSettingsResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _repository.GetAsync(userId, cancellationToken) ?? throw ProfileNotFound();
        }

        public async Task<UserSettingsResponse> UpdateAsync(
            Guid userId,
            UpdateUserSettingsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            if (request.AllowAskBack is { } allow
                && !await _repository.SetAllowAskBackAsync(userId, allow, cancellationToken))
            {
                throw ProfileNotFound();
            }

            return await GetAsync(userId, cancellationToken);
        }

        private static AppException ProfileNotFound()
            => new(404, ApiErrorCodes.NotFound, "User profile not found.");
    }
}
