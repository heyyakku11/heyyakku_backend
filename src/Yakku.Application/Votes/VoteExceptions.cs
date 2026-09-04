using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.Application.Votes
{
    public static class VoteExceptions
    {
        public static AppException AlreadyVoted()
        {
            return new AppException(
                409,
                ApiErrorCodes.AlreadyVoted,
                "You have already voted in this poll.",
                field: null,
                errorMessage: "This guest has already voted in this poll.");
        }
    }
}
