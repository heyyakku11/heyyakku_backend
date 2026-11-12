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
        private readonly ISavedFunCardRepository _saved;

        public FunCardService(ICheckRepository repository, ICheckService checks, ISavedFunCardRepository saved)
        {
            _repository = repository;
            _checks = checks;
            _saved = saved;
        }

        public async Task<List<FunCardResponse>> ListAsync(Guid? userId, CancellationToken cancellationToken = default)
        {
            var cards = await _repository.GetFunCardsAsync(activeOnly: true, cancellationToken);
            var used = userId is null
                ? new Dictionary<Guid, UserCheck>()
                : await _repository.GetUserChecksByCheckAsync(userId.Value, cancellationToken);
            var saved = await SavedIdsAsync(userId, cancellationToken);

            return cards.Select(card => ToResponse(card, used.ContainsKey(card.Id), saved.Contains(card.Id))).ToList();
        }

        public async Task<List<FunCardTypeResponse>> ListTypesAsync(Guid? userId, CancellationToken cancellationToken = default)
        {
            var available = await AvailableAsync(userId, cancellationToken);

            // One entry per type, in the order the types first appear (oldest card first).
            return available
                .GroupBy(entry => entry.Card.CardThemeId)
                .Select(group =>
                {
                    var first = group.First().Card;
                    var theme = CheckThemeMapper.ToResponse(first);
                    return new FunCardTypeResponse
                    {
                        TypeId = theme.CheckTypeId,
                        Name = theme.CheckType ?? first.Title,
                        TotalCount = group.Count(),
                        Cards = group.Take(FunCardLimits.PreviewPerType)
                            .Select(entry => ToResponse(entry.Card, entry.Used, entry.Saved))
                            .ToList()
                    };
                })
                .ToList();
        }

        public async Task<List<FunCardResponse>> ListByTypeAsync(
            Guid? userId,
            Guid typeId,
            CancellationToken cancellationToken = default)
        {
            var available = await AvailableAsync(userId, cancellationToken);
            return available
                .Where(entry => entry.Card.CardThemeId == typeId)
                .Select(entry => ToResponse(entry.Card, entry.Used, entry.Saved))
                .ToList();
        }

        // Active cards the caller can still make, oldest first (signed-out callers have made none).
        private async Task<List<(Check Card, bool Used, bool Saved)>> AvailableAsync(Guid? userId, CancellationToken cancellationToken)
        {
            var cards = await _repository.GetFunCardsAsync(activeOnly: true, cancellationToken);
            var used = userId is null
                ? new Dictionary<Guid, UserCheck>()
                : await _repository.GetUserChecksByCheckAsync(userId.Value, cancellationToken);

            var saved = await SavedIdsAsync(userId, cancellationToken);

            return cards
                .Where(card => !used.ContainsKey(card.Id))
                .Select(card => (card, false, saved.Contains(card.Id)))
                .ToList();
        }

        private async Task<HashSet<Guid>> SavedIdsAsync(Guid? userId, CancellationToken cancellationToken) =>
            userId is null ? [] : await _saved.GetSavedIdsAsync(userId.Value, cancellationToken);

        public Task<UserCheckResponse> MakeAsync(
            Guid userId,
            MakeCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            return _checks.MakeAsync(userId, request, CheckKind.Fun, cancellationToken);
        }

        internal static FunCardResponse ToResponse(Check card, bool used, bool saved = false)
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
                IsSaved = saved,
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

            // A preview per type; the full list of one type is GetByTypeAsync ("see all").
            return themes.Select(theme => ToGroup(theme, cards, FunCardLimits.PreviewPerType)).ToList();
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

        private static AdminFunCardGroupResponse ToGroup(CardTheme theme, IEnumerable<Check> allCards, int? limit = null)
        {
            var cards = allCards.Where(card => card.CardThemeId == theme.Id).ToList();
            var shown = limit is null ? cards : cards.Take(limit.Value).ToList();

            return new AdminFunCardGroupResponse
            {
                TotalCount = cards.Count,
                Theme = new AdminFunCardGroupTheme
                {
                    FunCardTypeId = theme.Id,
                    Heading = theme.Name,
                    BackgroundColor = theme.BackgroundColor,
                    LabelColor = theme.LabelColor,
                    Tag = theme.Tag
                },
                Cards = shown.Select(card => new AdminFunCardItemResponse
                {
                    Id = card.Id,
                    Question = card.Question,
                    Options = FunCardService.ToOptions(card),
                    IsActive = card.IsActive,
                    CreatedAt = card.CreatedAt
                }).ToList()
            };
        }

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
