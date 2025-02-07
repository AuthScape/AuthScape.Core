namespace AuthScape.Marketplace.Models
{
    public class SearchParams
    {
        public long? UserId { get; set; }
        public long? OemCompanyId { get; set; } // use this for a specific company
        public int PlatformId { get; set; } = 1;
        public int PageNumber { get; set; } = 0;
        public int PageSize { get; set; } = 20;
        public List<SearchParamFilter>? SearchParamFilters { get; set; }
        public SearchParamFilter? LastFilterSelected { get; set; }

        public List<CategoryFilters>? CategoryFilters { get; set; }
    }

    public class SearchChainOfCommands
    {
        public string Category { get; set; }
        public string Option { get; set; }
        public bool IsShould { get; set; }
    }


    public class SearchParamFilter
    {
        public string Category { get; set; }
        public string Option { get; set; }
    }


    public class CategoryFilters
    {
        public string Category { get; set; }
        public List<CategoryFilterOption> Options { get; set; }
    }

    public class CategoryFilterOption
    {
        public string Name { get; set; }
        public bool IsChecked { get; set; }
        public int Count { get; set; }
    }
}