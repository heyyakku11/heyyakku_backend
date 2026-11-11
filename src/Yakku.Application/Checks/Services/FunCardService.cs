using FluentValidation;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Checks.Services
{
    /// <summary>
    /// Just-for-fun cards: system-made cards with no live or expiry date. They are stored as checks of kind Fun,
    /// so making one, sharing it and collecting answers all use the check machinery unchanged.
    /// </summary>
    public class FunCardService : IFunCardService
    {
        private readonly ICheckRepository _repository;
        private readonly ICheckService _checks;

        public FunCardService(ICheckRepository repository, ICheckService checks)
        {
            _repository = repository;
            _checks = checks;
        }

        public async Task<List<FunCardResponse>> ListAsync(Guid? userId, CancellationToken cancellationToken = default)
        {
            var cards = await _repository.GetFunCardsAsync(activeOnly: true, cancellationToken);
            var used = userId is null
                ? new Dictionary<Guid, UserCheck>()
                : await _repository.GetUserChecksByCheckAsync(userId.Value, cancellationToken);

            return cards.Select(card =>
            {
                var theme = CheckThemeMapper.ToResponse(card);
                return new FunCardResponse
                {
                    Id = card.Id,
                    Title = card.Title,
                    Question = card.Question,
                    BackgroundColor = card.BackgroundColor,
                    Options = ToOptions(card),
                    Used = used.ContainsKey(card.Id),
                    CheckType = theme.CheckType,
                    Theme = theme
                };
            }).ToList();
        }

        public Task<UserCheckResponse> MakeAsync(
            Guid userId,
            MakeCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            return _checks.MakeAsync(userId, request, CheckKind.Fun, cancellationToken);
        }

        internal static List<CheckOptionResponse> ToOptions(Check card) =>
            card.Options
                .OrderBy(option => option.SortOrder)
                .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                .ToList();
    }

    /// <summary>Admin management of just-for-fun cards. The look always comes from the just-for-fun theme.</summary>
    public class AdminFunCardService : IAdminFunCardService
    {
        private readonly ICheckRepository _repository;
        private readonly ICheckThemeRepository _themes;
        private readonly IValidator<SaveFunCardRequest> _validator;

        public AdminFunCardService(
            ICheckRepository repository,
            ICheckThemeRepository themes,
            IValidator<SaveFunCardRequest> validator)
        {
            _repository = repository;
            _themes = themes;
            _validator = validator;
        }

        public async Task<CheckThemeDefinitionResponse> GetThemeAsync(CancellationToken cancellationToken = default)
        {
            var theme = await GetFunThemeAsync(cancellationToken);
            return new CheckThemeDefinitionResponse
            {
                Id = theme.Id,
                Name = theme.Name,
                BackgroundColor = theme.BackgroundColor,
                LabelColor = theme.LabelColor,
                Tag = theme.Tag
            };
        }

        public async Task<List<AdminFunCardResponse>> ListAsync(CancellationToken cancellationToken = default)
        {
            var cards = await _repository.GetFunCardsAsync(activeOnly: false, cancellationToken);
            return cards.Select(ToResponse).ToList();
        }

        public async Task<AdminFunCardResponse> CreateAsync(
            SaveFunCardRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);
            var theme = await GetFunThemeAsync(cancellationToken);

            var card = Check.CreateFun(
                theme.Name,
                request.Question.Trim(),
                theme.BackgroundColor,
                request.Options.Select(option => option.Trim()),
                theme.Id);
            if (!request.IsActive)
            {
                card.Update(null, card.Title, card.Question, card.BackgroundColor, card.Sticker, false);
            }

            await _repository.AddCheckAsync(card, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);

            return ToResponse(await _repository.GetForUpdateAsync(card.Id, cancellationToken) ?? card);
        }

        public async Task<AdminFunCardResponse> UpdateAsync(
            Guid id,
            SaveFunCardRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var card = await _repository.GetForUpdateAsync(id, cancellationToken);
            if (card is null || card.Kind != CheckKind.Fun)
            {
                throw CheckExceptions.NotFound();
            }

            var options = request.Options.Select(option => option.Trim()).ToList();
            var optionsChanged = !card.Options
                .OrderBy(option => option.SortOrder)
                .Select(option => option.Text)
                .SequenceEqual(options);

            // Answers point at options, so the wording can only change while nobody has made the card.
            var used = await _repository.HasUserChecksAsync(id, cancellationToken);
            if (used && (optionsChanged || card.Question != request.Question.Trim()))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This card has already been used. Only its active state can change.");
            }

            if (!used && optionsChanged)
            {
                card.ReplaceOptions(options);
            }

            card.Update(
                null,
                card.Title,
                used ? card.Question : request.Question.Trim(),
                card.BackgroundColor,
                card.Sticker,
                request.IsActive);

            await _repository.SaveChecksAsync(cancellationToken);
            return ToResponse(card);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var card = await _repository.GetForUpdateAsync(id, cancellationToken);
            if (card is null || card.Kind != CheckKind.Fun)
            {
                throw CheckExceptions.NotFound();
            }

            if (await _repository.HasUserChecksAsync(id, cancellationToken))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This card has already been used and cannot be deleted. Deactivate it instead.");
            }

            await _repository.RemoveCheckAsync(card, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);
        }

        private async Task<CardTheme> GetFunThemeAsync(CancellationToken cancellationToken)
        {
            var themes = await _themes.GetByKindAsync(CardThemeKind.Fun, cancellationToken);
            return themes.FirstOrDefault()
                ?? throw new AppException(
                    500,
                    ApiErrorCodes.InternalServerError,
                    "The just-for-fun card theme is not set up.");
        }

        private static AdminFunCardResponse ToResponse(Check card) => new()
        {
            Id = card.Id,
            Title = card.Title,
            Question = card.Question,
            BackgroundColor = card.BackgroundColor,
            IsActive = card.IsActive,
            Options = FunCardService.ToOptions(card),
            Theme = CheckThemeMapper.ToResponse(card),
            CreatedAt = card.CreatedAt
        };
    }
}
