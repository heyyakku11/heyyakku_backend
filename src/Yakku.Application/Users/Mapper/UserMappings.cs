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
                    AnonymousName = user.AnonymousName
                },
                Account = new UserAccountDetails
                {
                    CurrentPlan = new UserPlanResponse
                    {
                        Code = user.UserPlan?.PlanCode ?? Plan.FreeCode,
                        ActivatedAt = user.UserPlan?.PlanActivatedAt,
                        ExpiresAt = user.UserPlan?.PlanExpiresAt,
                        IsExpired = user.UserPlan is not null && !user.IsPlanActive(DateTime.UtcNow)
                    }
                }
            };
        }
    }
}
