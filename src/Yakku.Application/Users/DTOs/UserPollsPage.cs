using Yakku.Application.Common.Responses;

namespace Yakku.Application.Users.DTOs
{
    public class UserPollsPage
    {
        public List<UserPollResponse> Items { get; set; } = [];
        public PaginationMeta Meta { get; set; } = new();
    }
}
