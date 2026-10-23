using Yakku.Application.Polls;
using Yakku.Domain.Enums;

namespace Yakku.Application.Sharing
{
    /// <summary>Who is voting or viewing: a signed-in user or a guest. Both are identified by one id plus a role.</summary>
    public sealed record VoterIdentity(Guid Id, VoterRole Role)
    {
        public bool IsUser => Role == VoterRole.User;

        public static VoterIdentity ForUser(Guid userId) => new(userId, VoterRole.User);

        public static VoterIdentity ForGuest(Guid guestId) => new(guestId, VoterRole.Guest);

        public static VoterIdentity From(MediaOwner owner)
        {
            return owner.UserId is Guid userId
                ? ForUser(userId)
                : ForGuest(owner.GuestId ?? throw new ArgumentException("The caller has no identity.", nameof(owner)));
        }
    }
}
