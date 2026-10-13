namespace Yakku.Application.Email
{
    public sealed record EmailJob(
        Guid ChallengeId,
        string RecipientEmail,
        string Otp);
}
