namespace Yakku.Application.Common.Responses
{
    public class PaginationMeta
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }

        public static PaginationMeta Create(int page, int pageSize, int totalCount)
        {
            var totalPages = pageSize <= 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            return new PaginationMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        public static PaginationMeta ForCursor(int pageSize, string? nextCursor, bool hasMore)
        {
            return new PaginationMeta
            {
                PageSize = pageSize,
                NextCursor = nextCursor,
                HasMore = hasMore
            };
        }
    }
}
