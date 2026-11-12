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

            return cards.Select(card => ToResponse(card, used.ContainsKey(card.Id))).ToList();
        }

        public Task<UserCheckResponse> MakeAsync(
            Guid userId,
            MakeCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            return _checks.MakeAsync(userId, request, CheckKind.Fun, cancellationToken);
        }

        internal static FunCardResponse ToResponse(Check card, bool used)
        {
            var theme = CheckThemeMapper.ToResponse(card);
            return new FunCardResponse
            {
                Id = card.Id,
                Title = card.Title,
                Question = card.Question,
                BackgroundColor = card.BackgroundColor,
                Options = ToOptions(card),
                Used = used,
                CheckType = theme.CheckType,
                Theme = theme
            };
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
        private readonly IValidator<CreateCheckThemeRequest> _themeValidator;

        public AdminFunCardService(
            ICheckRepository repository,
            ICheckThemeRepository themes,
            IValidator<SaveFunCardRequest> validator,
            IValidator<CreateCheckThemeRequest> themeValidator)
        {
            _repository = repository;
            _themes = themes;
            _validator = validator;
            _themeValidator = themeValidator;
        }

        public async Task<List<CheckThemeDefinitionResponse>> ListThemesAsync(CancellationToken cancellationToken = default)
        {
            var themes = await _themes.GetByKindAsync(CardThemeKind.Fun, cancellationToken);
            return themes.Select(ToThemeResponse).ToList();
        }

        public async Task<CheckThemeDefinitionResponse> CreateThemeAsync(
            CreateCheckThemeRequest request,
            CancellationToken cancellationToken = default)
        {
            await _themeValidator.ValidateAndThrowAsync(request, cancellationToken);

            var tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();
            var theme = CardTheme.Create(
                CardThemeKind.Fun,
                request.Name.Trim(),
                request.BackgroundColor.ToUpperInvariant(),
                request.LabelColor.ToUpperInvariant(),
                tag);

            if (!await _themes.TryAddTypeAsync(theme, cancellationToken))
            {
                throw new AppException(409, ApiErrorCodes.Conflict, $"A fun card type named \"{theme.Name}\" already exists.", "name");
            }

            return ToThemeResponse(theme);
        }

        public async Task<List<AdminFunCardGroupResponse>> ListAsync(CancellationToken cancellationToken = default)
        {
            var themes = await _themes.GetByKindAsync(CardThemeKind.Fun, cancellationToken);
            var cards = await _repository.GetFunCardsAsync(activeOnly: false, cancellationToken);

            return themes.Select(theme => ToGroup(theme, cards)).ToList();
        }

        public async Task<AdminFunCardGroupResponse> GetByTypeAsync(
            Guid funCardTypeId,
            CancellationToken cancellationToken = default)
        {
            var themes = await _themes.GetByKindAsync(CardThemeKind.Fun, cancellationToken);
            var theme = themes.FirstOrDefault(t => t.Id == funCardTypeId)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Fun card type not found.");

            var cards = await _repository.GetFunCardsAsync(activeOnly: false, cancellationToken);
            return ToGroup(theme, cards);
        }

        private static AdminFunCardGroupResponse ToGroup(CardTheme theme, IEnumerable<Check> allCards) => new()
        {
            Theme = new AdminFunCardGroupTheme
            {
                FunCardTypeId = theme.Id,
                Heading = theme.Name,
                BackgroundColor = theme.BackgroundColor,
                LabelColor = theme.LabelColor,
                Tag = theme.Tag
            },
            Cards = allCards
                .Where(card => card.CardThemeId == theme.Id)
                .Select(card => new AdminFunCardItemResponse
                {
                    Id = card.Id,
                    Question = card.Question,
                    Options = FunCardService.ToOptions(card),
                    IsActive = card.IsActive,
                    CreatedAt = card.CreatedAt
                }).ToList()
        };

        public async Task<AdminFunCardCreatedResponse> CreateAsync(
            SaveFunCardRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);
            var themes = await _themes.GetByKindAsync(CardThemeKind.Fun, cancellationToken);
            var theme = themes.FirstOrDefault(t => t.Id == request.FunCardTypeId)
                ?? throw new AppException(400, ApiErrorCodes.ValidationError, "Unknown fun card type.", "funCardTypeId");

            var card = Check.CreateFun(
                theme.Name,
                request.Question.Trim(),
                theme.BackgroundColor,
                request.Options.Select(option => option.Trim()),
                theme.Id);
            if (!request.IsActive)
            {
                card.Update(null, card.Title, card.Question, card.BackgroundColor, false);
            }

            await _repository.AddCheckAsync(card, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);

            return new AdminFunCardCreatedResponse
            {
                Title = card.Title,
                Question = card.Question,
                IsActive = card.IsActive
            };
        }

        private static CheckThemeDefinitionResponse ToThemeResponse(CardTheme theme) => new()
        {
            Id = theme.Id,
            Name = theme.Name,
            BackgroundColor = theme.BackgroundColor,
            LabelColor = theme.LabelColor,
            Tag = theme.Tag
        };


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
