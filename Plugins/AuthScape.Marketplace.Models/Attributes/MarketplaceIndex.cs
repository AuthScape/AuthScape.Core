namespace AuthScape.Marketplace.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MarketplaceIndex : Attribute
    {
        public ProductCardCategoryType ProductCardCategoryType { get; }
        public string? ParentCategory { get; }
        public string? CategoryName { get; }
        public MarketplaceIndex(ProductCardCategoryType ProductCardCategoryType, string? categoryName = null, string? ParentCategory = null)
        {
            CategoryName = categoryName;
            this.ProductCardCategoryType = ProductCardCategoryType;
            this.ParentCategory = ParentCategory;
        }
    }
}