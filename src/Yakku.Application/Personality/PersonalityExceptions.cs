using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.Application.Personality
{
    public static class PersonalityExceptions
    {
        public static AppException CardNotFound() =>
            new(404, ApiErrorCodes.NotFound, "Personality card not found");

        public static AppException InvalidTraits() =>
            new(400, ApiErrorCodes.ValidationError, "One or more traits are invalid or no longer available.", "traitIds");

        public static AppException SelfAssessmentAlreadySubmitted() =>
            new(409, ApiErrorCodes.Conflict, "You have already submitted your self-assessment.");

        public static AppException CannotVoteOnOwnCard() =>
            new(403, ApiErrorCodes.Forbidden, "You cannot vote on your own personality card.");

        public static AppException AlreadyVoted() =>
            new(409, ApiErrorCodes.AlreadyVoted, "You have already voted on this personality card.");

        public static AppException NotCollectingVotes() =>
            new(409, ApiErrorCodes.Conflict, "This personality card is not accepting votes.");

        public static AppException ResultsNotAvailable() =>
            new(409, ApiErrorCodes.Conflict, "Results are available once the card has received all its votes.");

        public static AppException OnlyOwnerCanSeeResults() =>
            new(403, ApiErrorCodes.Forbidden, "Only the card owner can see the results.");
    }
}
