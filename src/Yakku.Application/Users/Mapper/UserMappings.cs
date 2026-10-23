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
                Status = user.Status.ToString(),
                PhoneNumber = user.PhoneNumber,
                Name = user.Profile?.Name ?? string.Empty,
                AnonymousName = user.Profile?.AnonymousName ?? string.Empty,
                LoginAt = user.LoginAt,
                AvatarUrl = null,
                CurrentPlan = new UserPlanResponse
                {
                    Id = user.PlanId,
                    Name = user.Plan?.Name ?? "Free"
                }
            };
        }
    }
}
