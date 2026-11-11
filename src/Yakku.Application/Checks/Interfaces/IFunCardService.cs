using Yakku.Application.Checks.DTOs;

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
        Task<List<AdminFunCardResponse>> ListAsync(CancellationToken cancellationToken = default);

        Task<AdminFunCardResponse> CreateAsync(SaveFunCardRequest request, CancellationToken cancellationToken = default);

        Task<AdminFunCardResponse> UpdateAsync(Guid id, SaveFunCardRequest request, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
