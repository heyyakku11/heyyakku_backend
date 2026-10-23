using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Registry of every share code. The code is its primary key, so it is unique across polls and checks and one
    /// lookup resolves it to a poll or a made check (<see cref="UserCheck"/>). The code is also kept on the content
    /// row; both are written in the same transaction.
    /// </summary>
    public class ShareCode
    {
        public string Code { get; private set; } = string.Empty;
        public ShareContentType ContentType { get; private set; }
        public Guid? PollId { get; private set; }
        public Guid? UserCheckId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private ShareCode()
        {
        }

        public static ShareCode ForPoll(Poll poll)
        {
            return new ShareCode
            {
                Code = poll.ShareCode,
                ContentType = ShareContentType.Poll,
                PollId = poll.Id,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static ShareCode ForCheck(UserCheck userCheck)
        {
            return new ShareCode
            {
                Code = userCheck.ShareCode,
                ContentType = ShareContentType.Check,
                UserCheckId = userCheck.Id,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
