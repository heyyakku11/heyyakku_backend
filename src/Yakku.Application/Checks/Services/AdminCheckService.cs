using FluentValidation;
using Yakku.Application.Caching;
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
        private readonly IResponseCache _cache;

        public AdminCheckService(
            ICheckRepository repository,
            IValidator<SaveCheckRequest> validator,
            ICheckThemeService themes,
            IResponseCache? cache = null)
        {
            _repository = repository;
            _validator = validator;
            _themes = themes;
            _cache = cache ?? new NullResponseCache();
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

            var theme = await _themes.ResolveForNewCheckAsync(request.CheckTypeId.Value, cancellationToken);

            var week = CheckWeek.StartOf(request.WeekStart);
            var check = Check.Create(
                week,
                theme.Config.Heading,
                request.Question.Trim(),
                request.Options.Select(option => option.Trim()),
                theme.CardThemeId);
            if (!request.IsActive)
            {
                check.Update(week, check.Title, check.Question, false);
            }

            await _repository.AddCheckAsync(check, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);
            await _cache.RemoveAsync(ResponseCacheKeys.ChecksWeekAnon(week), cancellationToken);

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

            ResolvedCheckTheme? newTheme = null;
            if (request.CheckTypeId is { } requested && requested != check.CardThemeId)
            {
                newTheme = await _themes.ResolveForNewCheckAsync(requested, cancellationToken);
            }

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
                check.AssignTheme(newTheme.CardThemeId, newTheme.Config.Heading);
            }

            check.Update(
                week,
                check.Title,
                used ? check.Question : request.Question.Trim(),
                request.IsActive);

            await _repository.SaveChecksAsync(cancellationToken);
            await _cache.RemoveAsync(ResponseCacheKeys.ChecksWeekAnon(week), cancellationToken);

            if (newTheme is not null)
            {
                check = await _repository.GetForUpdateAsync(id, cancellationToken) ?? check;
            }

            return ToResponse(check);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var check = await _repository.GetForUpdateAsync(id, cancellationToken);
            if (check is null || check.IsFun)
            {
                throw CheckExceptions.NotFound();
            }

            var week = check.LiveWeekStart;
            await _repository.DeleteWithUsageAsync(check, cancellationToken);
            if (week is { } liveWeek)
            {
                await _cache.RemoveAsync(ResponseCacheKeys.ChecksWeekAnon(liveWeek), cancellationToken);
            }
        }

        private static AdminCheckResponse ToResponse(Check check)
        {
            var theme = CheckThemeMapper.ToResponse(check);
            return new AdminCheckResponse
            {
                Id = check.Id,
                WeekStart = check.LiveWeekStart ?? default,
                Title = check.Title,
                Question = check.Question,
                BackgroundColor = theme.Config.BackgroundColor,
                LabelColor = theme.Config.LabelColor,
                IsActive = check.IsActive,
                Status = check.LiveWeekStart is { } live && live <= CheckWeek.Current() ? "live" : "upcoming",
                Options = check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                    .ToList()
            };
        }
    }
}
