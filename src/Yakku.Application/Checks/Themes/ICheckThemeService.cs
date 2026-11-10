using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Themes
{
    public interface ICheckThemeRepository
    {
        Task<List<CheckType>> GetAllTypesAsync(CancellationToken cancellationToken = default);
        Task<CheckType?> GetTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Adds the type and saves. False when the name is already taken (including a concurrent insert).</summary>
        Task<bool> TryAddTypeAsync(CheckType type, CancellationToken cancellationToken = default);
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

    public sealed record ResolvedCheckTheme(Guid CheckTypeId, CheckThemeConfig Config);
}
