using Yakku.Application.Common.Responses;

namespace Yakku.Application.Polls.DTOs
{
    public class CreatorPollsPage
    {
        public List<PollSummaryResponse> Items { get; set; } = [];
        public PaginationMeta Meta { get; set; } = new();
    }
}
