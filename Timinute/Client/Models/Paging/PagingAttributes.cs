namespace Timinute.Client.Models.Paging
{
    public class PagingAttributes
    {
        public int TotalPageCount { get; set; } = 1;
        public int Count { get; set; } = 1;
        public int TotalCount { get; set; } = 1;
        public int PageCount { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int CurrentPage { get; set; } = 1;
    }
}
