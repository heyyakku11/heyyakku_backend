using Yakku.Application.Community.DTOs;
using Yakku.Application.Community.Interfaces;

namespace Yakku.Application.Community.Services
{
    public class CommunityWaitlistService : ICommunityWaitlistService
    {
        private readonly ICommunityWaitlistRepository _repository;

        public CommunityWaitlistService(ICommunityWaitlistRepository repository)
        {
            _repository = repository;
        }

        public async Task<CommunityWaitlistResponse> JoinAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var (entry, added) = await _repository.AddIfMissingAsync(userId, cancellationToken);
            return new CommunityWaitlistResponse
            {
                Joined = true,
                AlreadyJoined = !added,
                JoinedAt = entry.CreatedAt
            };
        }
    }
}
