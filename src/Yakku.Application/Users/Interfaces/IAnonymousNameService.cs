using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public interface IAnonymousNameService
    {
        /// <summary>Gives the signed-in user a new unique anonymous name and returns it.</summary>
        Task<AnonymousNameResponse> RegenerateAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
