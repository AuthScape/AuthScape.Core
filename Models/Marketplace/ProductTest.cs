using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;
using AuthScape.Marketplace.Models.CSVReader;

namespace Models.Marketplace
{
    public class ProductTest : BaseProductCSVReader
    {
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string? Description { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.StringField, "Brand Name")]
        public string? BrandName { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string? MainPhoto { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string? WebsiteUrl { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None, "Category", "ParentCategory")]
        public string? category1 { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string? category2 { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string? category3 { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.StringField)]
        public string? ParentCategory { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string? PartNumber { get; set; }
    }
}
