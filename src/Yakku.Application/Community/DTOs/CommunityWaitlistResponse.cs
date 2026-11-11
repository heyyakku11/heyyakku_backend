namespace Yakku.Application.Community.DTOs
{
    public class CommunityWaitlistResponse
    {
        /// <summary>Always true: the caller is in the queue.</summary>
        public bool Joined { get; set; }

        /// <summary>True when the caller had already asked earlier; nothing new was added.</summary>
        public bool AlreadyJoined { get; set; }

        /// <summary>When the caller first asked to be notified.</summary>
        public DateTime JoinedAt { get; set; }
    }
}
