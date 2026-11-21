namespace Yakku.Domain
{
    /// <summary>
    /// Shared caps for poll/check questions, options (opinions), custom vote answers,
    /// voter reasons, and report free-text. Clients and validators must stay aligned.
    /// </summary>
    public static class ContentLengthLimits
    {
        /// <summary>Poll and check questions.</summary>
        public const int MaxQuestionLength = 200;

        /// <summary>Creator options / opinions, custom answers, vote reasons, and report details.</summary>
        public const int MaxAnswerLength = 120;
    }
}
