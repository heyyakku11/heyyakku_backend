using Yakku.Application.Users.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Users.Mapper
{
    internal static class UserMappings
    {
        public static UserProfileResponse ToProfileResponse(this User user)
        {
            return new UserProfileResponse
            {
                Id = user.Id,
                Status = user.Status.ToString(),
                Email = user.Email,
                DisplayName = user.Profile?.DisplayName ?? string.Empty,
                LastLoginAt = user.LastLoginAt,
                AvatarUrl = user.Profile?.AvatarUrl
            };
        }
    }
}
