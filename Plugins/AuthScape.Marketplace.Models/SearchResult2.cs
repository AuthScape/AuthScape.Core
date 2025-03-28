namespace AuthScape.Marketplace.Models
{
    public class SearchResult2
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<CategoryResponse> Categories { get; set; }
        public List<List<ProductResult>> Products { get; set; }
        public IEnumerable<CategoryFilters> Filters { get; set; }
        public Guid TrackingId { get; set; }
    }
}