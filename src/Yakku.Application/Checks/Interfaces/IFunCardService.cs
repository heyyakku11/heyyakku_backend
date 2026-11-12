using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Themes;

namespace Yakku.Application.Checks.Interfaces
{
    /// <summary>"Just for fun" cards for the app: list them and make one your own.</summary>
    public interface IFunCardService
    {
        /// <summary>Every active just-for-fun card, oldest first, with the caller's used flag.</summary>
        Task<List<FunCardResponse>> ListAsync(Guid? userId, CancellationToken cancellationToken = default);

        /// <summary>Makes a fun card the caller's own shareable card, once per card. Same flow as making a check.</summary>
        Task<UserCheckResponse> MakeAsync(Guid userId, MakeCheckRequest request, CancellationToken cancellationToken = default);
    }

    public interface IAdminFunCardService
    {
        /// <summary>All fun card types (themes).</summary>
        Task<List<CheckThemeDefinitionResponse>> ListThemesAsync(CancellationToken cancellationToken = default);

        /// <summary>Defines a new fun card type. 409 when the name exists.</summary>
        Task<CheckThemeDefinitionResponse> CreateThemeAsync(CreateCheckThemeRequest request, CancellationToken cancellationToken = default);

        /// <summary>Every fun card type with its cards (active or not), grouped by type.</summary>
        Task<List<AdminFunCardGroupResponse>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>One fun card type with all its cards (active or not). 404 when the type does not exist.</summary>
        Task<AdminFunCardGroupResponse> GetByTypeAsync(Guid funCardTypeId, CancellationToken cancellationToken = default);

        Task<AdminFunCardCreatedResponse> CreateAsync(SaveFunCardRequest request, CancellationToken cancellationToken = default);

    }
}
