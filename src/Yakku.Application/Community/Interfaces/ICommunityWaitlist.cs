using Yakku.Application.Community.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Community.Interfaces
{
    public interface ICommunityWaitlistRepository
    {
        /// <summary>
        /// Adds the user to the queue. Returns the entry and whether it was newly added; when the user is
        /// already queued (including a concurrent request) returns the existing entry with false.
        /// </summary>
        Task<(CommunityWaitlistEntry Entry, bool Added)> AddIfMissingAsync(Guid userId, CancellationToken cancellationToken = default);
    }

    public interface ICommunityWaitlistService
    {
        /// <summary>Registers the caller to be notified when the community goes live. Safe to call again.</summary>
        Task<CommunityWaitlistResponse> JoinAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
