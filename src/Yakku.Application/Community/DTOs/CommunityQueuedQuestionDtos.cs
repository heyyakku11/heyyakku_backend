namespace Yakku.Application.Community.DTOs
{
    public class UpsertCommunityQueuedQuestionRequest
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class CommunityQueuedQuestionTraitResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CommunityQueuedQuestionResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Owner's crowd-top personality traits when their completed card has ShareToCommunity;
        /// otherwise empty.
        /// </summary>
        public List<CommunityQueuedQuestionTraitResponse> PersonalityTraits { get; set; } = new();
    }
}
