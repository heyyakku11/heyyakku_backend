using FluentValidation;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Checks.Services
{
    public class CheckThemeService : ICheckThemeService
    {
        private readonly ICheckThemeRepository _repository;
        private readonly IValidator<CreateCheckThemeRequest> _validator;

        public CheckThemeService(ICheckThemeRepository repository, IValidator<CreateCheckThemeRequest> validator)
        {
            _repository = repository;
            _validator = validator;
        }

        public async Task<List<CheckThemeDefinitionResponse>> ListAsync(CancellationToken cancellationToken = default)
        {
            var types = await _repository.GetAllTypesAsync(cancellationToken);
            return types.Select(ToResponse).ToList();
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

            return ToResponse(type);
        }

        public async Task<ResolvedCheckTheme> ResolveForNewCheckAsync(Guid checkTypeId, CancellationToken cancellationToken = default)
        {
            var type = await _repository.GetTypeByIdAsync(checkTypeId, cancellationToken)
                ?? throw new AppException(400, ApiErrorCodes.ValidationError, "Unknown check type.", "checkTypeId");

            return new ResolvedCheckTheme(type.Id, CheckThemeMapper.ConfigOf(type));
        }

        private static CheckThemeDefinitionResponse ToResponse(CardTheme type) => new()
        {
            Id = type.Id,
            Name = type.Name,
            BackgroundColor = type.BackgroundColor,
            LabelColor = type.LabelColor,
            Tag = type.Tag
        };
    }
}
