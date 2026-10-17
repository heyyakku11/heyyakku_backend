using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.Application.Checks
{
    public static class CheckExceptions
    {
        public static AppException NotFound()
        {
            return new AppException(404, ApiErrorCodes.NotFound, "Check not found");
        }

        public static AppException AlreadyUsed()
        {
            return new AppException(
                409,
                ApiErrorCodes.AlreadyUsed,
                "You have already used this check.");
        }
    }
}
