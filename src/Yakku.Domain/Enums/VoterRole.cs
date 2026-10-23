namespace Yakku.Domain.Enums
{
    /// <summary>Whether <c>poll_votes.voter_id</c> holds a Yakku user id or a guest id.</summary>
    public enum VoterRole
    {
        User = 0,
        Guest = 1
    }
}
