namespace Yakku.Application.Polls
{
    public sealed record MediaOwner(Guid? UserId, Guid? GuestId)
    {
        public static MediaOwner ForUser(Guid userId) => new(userId, null);

        public static MediaOwner ForGuest(Guid guestId) => new(null, guestId);

        public bool IsUser => UserId is Guid;

        public bool IsGuest => GuestId is Guid;

        public bool Owns(Domain.Entities.MediaUploadSession session)
        {
            if (UserId is Guid userId)
            {
                return session.UserId == userId;
            }

            if (GuestId is Guid guestId)
            {
                return session.GuestId == guestId;
            }

            return false;
        }
    }
}
