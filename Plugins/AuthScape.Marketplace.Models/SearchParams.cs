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

        public List<SearchParamFilter>? Query { get; set; }

        public List<CategoryFilters>? CategoryFilters { get; set; }

        public string? TextSearch { get; set; }

        /// <summary>
        /// Minimum price filter (inclusive)
        /// </summary>
        public decimal? MinPrice { get; set; }

        /// <summary>
        /// Maximum price filter (inclusive)
        /// </summary>
        public decimal? MaxPrice { get; set; }

        /// <summary>
        /// The field name to use for price filtering (defaults to "Price")
        /// </summary>
        public string PriceField { get; set; } = "Price";

        public string? Container { get; set; }
        public string? StorageConnectionString { get; set; }
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
        public string Subcategory { get; set; }
        public string Option { get; set; }
    }


    public class CategoryFilters
    {
        public string Category { get; set; }
        public IEnumerable<CategoryFilterOption> Options { get; set; }

        /// <summary>
        /// Total number of unique filter options available (before limiting)
        /// </summary>
        public int TotalOptionsCount { get; set; }

        /// <summary>
        /// Indicates if there are more options than returned (for "Show more" UI)
        /// </summary>
        public bool HasMoreOptions { get; set; }

        /// <summary>
        /// The type of filter category (e.g., StringField, ColorField)
        /// </summary>
        public ProductCardCategoryType? CategoryType { get; set; }

        /// <summary>
        /// For ColorField type: Dictionary mapping color names to hex values.
        /// Allows custom color definitions beyond CSS standard colors.
        /// </summary>
        public Dictionary<string, string>? ColorHexMapping { get; set; }

        /// <summary>
        /// Display order for the filter in the UI. Lower numbers appear first.
        /// Filters without an order (0) will appear after ordered filters.
        /// </summary>
        public int Order { get; set; } = 0;
    }

    public class CategoryFilterOption
    {
        public string Name { get; set; }
        public bool IsChecked { get; set; }
        public int Count { get; set; }

        public IEnumerable<CategoryFilterOption>? Subcategories { get; set; }
    }

    /// <summary>
    /// Request for searching/paginating filter options within a specific category
    /// </summary>
    public class FilterOptionsRequest
    {
        public int PlatformId { get; set; } = 1;
        public long? OemCompanyId { get; set; }

        /// <summary>
        /// The filter category to search (e.g., "Brand", "Category", "Color")
        /// </summary>
        public string FilterCategory { get; set; }

        /// <summary>
        /// Optional search term to filter options (e.g., "Abbott" to find brands containing "Abbott")
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Number of options per page
        /// </summary>
        public int PageSize { get; set; } = 50;

        /// <summary>
        /// Currently selected filter options (to show them first/marked as checked)
        /// </summary>
        public List<string>? SelectedOptions { get; set; }

        /// <summary>
        /// Active filters from OTHER categories (for independent faceting).
        /// This allows showing filter options that exist in the filtered product set.
        /// </summary>
        public List<SearchParamFilter>? ActiveFilters { get; set; }
    }

    /// <summary>
    /// Response for filter options search
    /// </summary>
    public class FilterOptionsResponse
    {
        public string Category { get; set; }
        public List<CategoryFilterOption> Options { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasMorePages { get; set; }
    }

    /// <summary>
    /// Analytics data for marketplace impressions and clicks
    /// </summary>
    public class MarketplaceAnalytics
    {
        public int PlatformId { get; set; }
        public long? OemCompanyId { get; set; }
        public int PeriodDays { get; set; }
        public int TotalImpressions { get; set; }
        public int TotalClicks { get; set; }
        public double ClickThroughRate { get; set; }
        public List<PopularProduct> PopularProducts { get; set; } = new();
        public List<PopularFilter> PopularFilters { get; set; } = new();
        public List<DailyTrend> DailyTrends { get; set; } = new();
    }

    public class PopularProduct
    {
        public string ProductId { get; set; }
        public int Clicks { get; set; }
    }

    public class PopularFilter
    {
        public string Category { get; set; }
        public string Option { get; set; }
        public int Usage { get; set; }
    }

    public class DailyTrend
    {
        public DateTime Date { get; set; }
        public int Impressions { get; set; }
        public int Clicks { get; set; }
    }
}