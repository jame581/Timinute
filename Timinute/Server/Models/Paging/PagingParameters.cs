namespace Timinute.Server.Models.Paging
{
    public class PagingParameters
    {
        const int maxPageSize = 50;
        
        public int PageNumber { get; set; } = 1;
        
        private int pageSize = 10;
        
        public int PageSize
        {
            get
            {
                return pageSize;
            }
            set
            {
                pageSize = (value > maxPageSize) ? maxPageSize : value;
            }
        }
    }
}
