namespace AuthScape.Marketplace.Models
{
    public class SearchParams
    {
        public int PageNumber { get; set; } = 0;
        public int PageSize { get; set; } = 20;
        public List<SearchParamFilter>? SearchParamFilters { get; set; }
    }

    public class SearchParamFilter
    {
        public string Category { get; set; }
        public List<string> Options { get; set; }
    }
}