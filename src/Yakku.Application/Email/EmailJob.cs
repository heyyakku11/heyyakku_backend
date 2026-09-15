namespace Yakku.Application.Email
{
    public sealed record EmailJob(
        Guid EmailLogId,
        Guid ChallengeId,
        string RecipientEmail,
        string Otp);
}
