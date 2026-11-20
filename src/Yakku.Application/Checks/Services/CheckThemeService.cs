using FluentValidation;
using Microsoft.Extensions.Options;
using Yakku.Application.Caching;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Checks.Services
{
    public class CheckThemeService : ICheckThemeService
    {
        private readonly ICheckThemeRepository _repository;
        private readonly IValidator<CreateCheckThemeRequest> _validator;
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;

        public CheckThemeService(
            ICheckThemeRepository repository,
            IValidator<CreateCheckThemeRequest> validator,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null)
        {
            _repository = repository;
            _validator = validator;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
        }

        public Task<List<CheckThemeDefinitionResponse>> ListAsync(CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.CheckThemesAll,
                _cacheOptions.CheckThemes,
                async ct =>
                {
                    var types = await _repository.GetAllTypesAsync(ct);
                    return types.Select(ToResponse).ToList();
                },
                cancellationToken);
        }

        public async Task<CheckThemeDefinitionResponse> CreateAsync(
            CreateCheckThemeRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();
            var type = CardTheme.Create(
                CardThemeKind.Check,
                request.Name.Trim(),
                request.BackgroundColor.ToUpperInvariant(),
                request.LabelColor.ToUpperInvariant(),
                tag);

            if (!await _repository.TryAddTypeAsync(type, cancellationToken))
            {
                throw new AppException(409, ApiErrorCodes.Conflict, $"A check theme named \"{type.Name}\" already exists.", "name");
            }

            await _cache.RemoveAsync(ResponseCacheKeys.CheckThemesAll, cancellationToken);
            return ToResponse(type);
        }

        public async Task<ResolvedCheckTheme> ResolveForNewCheckAsync(Guid checkTypeId, CancellationToken cancellationToken = default)
        {
            var type = await _repository.GetTypeByIdAsync(checkTypeId, cancellationToken)
                ?? throw new AppException(400, ApiErrorCodes.ValidationError, "Unknown check type.", "checkTypeId");
            if (!type.IsActive)
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "This check type is inactive.", "checkTypeId");
            }

            return new ResolvedCheckTheme(type.Id, CheckThemeMapper.ConfigOf(type));
        }

        private static CheckThemeDefinitionResponse ToResponse(CardTheme type) => new()
        {
            Id = type.Id,
            Name = type.Name,
            BackgroundColor = type.BackgroundColor,
            LabelColor = type.LabelColor,
            Tag = type.Tag,
            IsActive = type.IsActive
        };
    }
}
