namespace Yakku.Application.Users.DTOs
{
    public class AccountDeletionResponse
    {
        public DateTime RequestedAt { get; set; }

        /// <summary>The account is permanently deleted at this time unless the user logs in again first.</summary>
        public DateTime ScheduledFor { get; set; }
    }
}
