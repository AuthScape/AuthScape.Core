namespace AuthScape.Marketplace.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MarketplaceIndex : Attribute
    {
        public ProductCardCategoryType ProductCardCategoryType { get; }
        public string? ParentCategory { get; }
        public string? CategoryName { get; }

        /// <summary>
        /// For ColorField type: JSON dictionary mapping color names to hex values.
        /// Example: "{\"Navy Blue\": \"#001f3f\", \"Forest Green\": \"#228B22\"}"
        /// If not provided, CSS color names are automatically mapped.
        /// </summary>
        public string? ColorHexMapping { get; set; }

        /// <summary>
        /// Display order for the filter in the UI. Lower numbers appear first.
        /// Filters without an order (0) will appear after ordered filters.
        /// </summary>
        public int Order { get; set; } = 0;

        public MarketplaceIndex(ProductCardCategoryType ProductCardCategoryType, string? categoryName = null, string? ParentCategory = null)
        {
            CategoryName = categoryName;
            this.ProductCardCategoryType = ProductCardCategoryType;
            this.ParentCategory = ParentCategory;
        }
    }
}