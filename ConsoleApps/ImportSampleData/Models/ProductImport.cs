using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;

namespace ImportSampleData.Models
{
    public class ProductCsv
    {
        public string Index { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Brand { get; set; }
        public string Category { get; set; }
        public string Price { get; set; }
        public string Currency { get; set; }
        public string Stock { get; set; }
        public string EAN { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public string Availability { get; set; }
    }


    public class ProductImport : BaseNLPProduct
    {
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Index { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.TextField)]
        public string Name { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.TextField)]
        public string Description { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Brand { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.StringField)]
        public string Category { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Price { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Currency { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Stock { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string EAN { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Color { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Size { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public string Availability { get; set; }
    }
}