using AuthScape.Marketplace.Models.Attributes;

namespace AuthScape.Marketplace.Models
{
    public class BaseNLPProduct
    {
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public long Id { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.StringField)]
        public string Name { get; set; }
        [MarketplaceIndex(ProductCardCategoryType.Int64Field)]
        public long Score { get; set; } // determines the placement when navigating
        [MarketplaceIndex(ProductCardCategoryType.None)]
        public int CardSize { get; set; } = 3;
    }
}