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
                        Code = user.Plan?.Code ?? Plan.FreeCode,
                        ActivatedAt = user.PlanActivatedAt,
                        ExpiresAt = user.PlanExpiresAt,
                        IsExpired = !user.IsPlanActive(DateTime.UtcNow)
                    }
                }
            };
        }
    }
}
