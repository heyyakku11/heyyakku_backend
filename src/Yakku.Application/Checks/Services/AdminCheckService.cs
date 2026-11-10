using FluentValidation;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Services
{
    /// <summary>
    /// Admin scheduling of checks: which checks go live in which week.
    /// Admins choose only the check type, question, options and week; the type decides every visual property.
    /// </summary>
    public class AdminCheckService : IAdminCheckService
    {
        private readonly ICheckRepository _repository;
        private readonly IValidator<SaveCheckRequest> _validator;
        private readonly ICheckThemeService _themes;

        public AdminCheckService(
            ICheckRepository repository,
            IValidator<SaveCheckRequest> validator,
            ICheckThemeService themes)
        {
            _repository = repository;
            _validator = validator;
            _themes = themes;
        }

        public async Task<List<AdminCheckResponse>> ListAsync(
            DateOnly? weekStart,
            CancellationToken cancellationToken = default)
        {
            var week = weekStart is null ? (DateOnly?)null : CheckWeek.StartOf(weekStart.Value);
            var checks = await _repository.GetAllAsync(week, cancellationToken);
            return checks.Select(ToResponse).ToList();
        }

        public async Task<AdminCheckResponse> CreateAsync(
            SaveCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            if (request.CheckTypeId is null || request.CheckTypeId == Guid.Empty)
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "Check type is required.", "checkTypeId");
            }

            // The type decides the look; the heading, color and sticker are also copied onto the check for older clients.
            var theme = await _themes.ResolveForNewCheckAsync(request.CheckTypeId.Value, cancellationToken);

            var week = CheckWeek.StartOf(request.WeekStart);
            var check = Check.Create(
                week,
                theme.Config.Heading,
                request.Question.Trim(),
                theme.Config.BackgroundColor,
                theme.Config.StickerLabel ?? string.Empty,
                request.Options.Select(option => option.Trim()),
                theme.CheckTypeId);
            if (!request.IsActive)
            {
                check.Update(week, check.Title, check.Question, check.BackgroundColor, check.Sticker, false);
            }

            // The check and its options are saved together in one SaveChanges (a single transaction).
            await _repository.AddCheckAsync(check, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);

            var saved = await _repository.GetForUpdateAsync(check.Id, cancellationToken) ?? check;
            return ToResponse(saved);
        }

        public async Task<AdminCheckResponse> UpdateAsync(
            Guid id,
            SaveCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var check = await _repository.GetForUpdateAsync(id, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            var week = CheckWeek.StartOf(request.WeekStart);

            var options = request.Options.Select(option => option.Trim()).ToList();
            var optionsChanged = !check.Options
                .OrderBy(option => option.SortOrder)
                .Select(option => option.Text)
                .SequenceEqual(options);

            // Omitting the type keeps the current one; only an explicit different type changes it.
            ResolvedCheckTheme? newTheme = null;
            if (request.CheckTypeId is { } requested && requested != check.CheckTypeId)
            {
                newTheme = await _themes.ResolveForNewCheckAsync(requested, cancellationToken);
            }

            // Votes point at options, so the content can only change while nobody has used the check.
            var used = await _repository.HasUserChecksAsync(id, cancellationToken);
            var contentChanged = optionsChanged ||
                check.Question != request.Question.Trim() ||
                newTheme is not null;
            if (used && contentChanged)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This check has already been used. Only its week and active state can change.");
            }

            if (!used && optionsChanged)
            {
                check.ReplaceOptions(options);
            }

            if (newTheme is not null)
            {
                check.AssignType(
                    newTheme.CheckTypeId,
                    newTheme.Config.Heading,
                    newTheme.Config.BackgroundColor,
                    newTheme.Config.StickerLabel);
            }

            check.Update(
                week,
                check.Title,
                used ? check.Question : request.Question.Trim(),
                check.BackgroundColor,
                check.Sticker,
                request.IsActive);

            await _repository.SaveChecksAsync(cancellationToken);

            if (newTheme is not null)
            {
                // Reload so the response carries the newly assigned type navigation.
                check = await _repository.GetForUpdateAsync(id, cancellationToken) ?? check;
            }

            return ToResponse(check);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var check = await _repository.GetForUpdateAsync(id, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            if (await _repository.HasUserChecksAsync(id, cancellationToken))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This check has already been used and cannot be deleted. Deactivate it instead.");
            }

            await _repository.RemoveCheckAsync(check, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);
        }

        private static AdminCheckResponse ToResponse(Check check)
        {
            var theme = CheckThemeMapper.ToResponse(check);
            return new AdminCheckResponse
            {
                Id = check.Id,
                WeekStart = check.LiveWeekStart,
                Title = check.Title,
                Question = check.Question,
                BackgroundColor = check.BackgroundColor,
                Sticker = check.Sticker,
                IsActive = check.IsActive,
                Status = check.LiveWeekStart <= CheckWeek.Current() ? "live" : "upcoming",
                Options = check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                    .ToList(),
                CheckType = theme.CheckType,
                CheckTypeId = theme.CheckTypeId,
                Theme = theme
            };
        }
    }
}
