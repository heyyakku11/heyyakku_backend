using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Auth
{
    internal static class ClaimsPrincipalExtensions
    {
        public static Guid GetRequiredUserId(this ClaimsPrincipal user)
        {
            if (!user.TryGetUserId(out var userId))
            {
                throw new AppException(
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.Unauthorized,
                    "Unauthorized.");
            }

            return userId;
        }

        public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(value, out userId) || userId == Guid.Empty)
            {
                userId = Guid.Empty;
                return false;
            }

            return true;
        }
    }
}
