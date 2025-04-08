namespace AuthScape.Marketplace.Models
{
    public class MarketplaceResponse
    {
        public int Total { get; set; }
        public List<CategoryResponse> Categories { get; set; }
        //public List<ProductCard> Products { get; set; }
    }
}