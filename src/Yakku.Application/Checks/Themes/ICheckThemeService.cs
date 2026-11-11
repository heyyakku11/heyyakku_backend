using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Checks.Themes
{
    public interface ICheckThemeRepository
    {
        /// <summary>The weekly-check themes, by name.</summary>
        Task<List<CardTheme>> GetAllTypesAsync(CancellationToken cancellationToken = default);

        /// <summary>The themes of one card kind, by name.</summary>
        Task<List<CardTheme>> GetByKindAsync(CardThemeKind kind, CancellationToken cancellationToken = default);

        /// <summary>A weekly-check theme by id; null when it does not exist or is another kind's.</summary>
        Task<CardTheme?> GetTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Adds the theme and saves. False when the name is already taken for its kind (including a concurrent insert).</summary>
        Task<bool> TryAddTypeAsync(CardTheme type, CancellationToken cancellationToken = default);
    }

    public interface ICheckThemeService
    {
        /// <summary>All themes, by name.</summary>
        Task<List<CheckThemeDefinitionResponse>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>Defines a new check theme (and the check type it belongs to). 409 when the name exists.</summary>
        Task<CheckThemeDefinitionResponse> CreateAsync(CreateCheckThemeRequest request, CancellationToken cancellationToken = default);

        /// <summary>Resolves a theme for a new check; 400 when it does not exist.</summary>
        Task<ResolvedCheckTheme> ResolveForNewCheckAsync(Guid checkTypeId, CancellationToken cancellationToken = default);
    }

    public sealed record ResolvedCheckTheme(Guid CardThemeId, CheckThemeConfig Config);
}
