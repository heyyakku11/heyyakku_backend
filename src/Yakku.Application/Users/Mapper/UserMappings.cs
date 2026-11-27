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
                Settings = new UserSettingsResponse
                {
                    AllowAskBack = user.Permissions?.AllowAskBack ?? true
                },
                Account = new UserAccountDetails
                {
                    CurrentPlan = new UserPlanResponse
                    {
                        Id = user.PlanId,
                        Code = user.Plan?.Code ?? Plan.FreeCode,
                        Name = user.Plan?.Name ?? "Free",
                        ActivatedAt = user.PlanActivatedAt,
                        ExpiresAt = user.PlanExpiresAt,
                        IsExpired = !user.IsPlanActive(DateTime.UtcNow)
                    }
                }
            };
        }
    }
}
