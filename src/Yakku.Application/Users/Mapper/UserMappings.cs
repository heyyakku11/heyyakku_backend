using Yakku.Application.Users.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Users.Mapper
{
    internal static class UserMappings
    {
        /// <summary>Maps what the user and profile rows hold; the stats are filled in by the caller.</summary>
        public static UserProfileResponse ToProfileResponse(this User user)
        {
            return new UserProfileResponse
            {
                Profile = new UserProfileDetails
                {
                    Name = user.Profile?.Name ?? string.Empty,
                    AnonymousName = user.Profile?.AnonymousName ?? string.Empty,
                    AvatarUrl = null
                },
                Account = new UserAccountDetails
                {
                    Status = user.Status.ToString(),
                    PhoneNumber = user.PhoneNumber,
                    LoginAt = user.LoginAt,
                    IsPushAllowed = user.Profile?.IsPushAllowed ?? true,
                    CurrentPlan = new UserPlanResponse
                    {
                        Id = user.PlanId,
                        Name = user.Plan?.Name ?? "Free"
                    }
                }
            };
        }
    }
}
