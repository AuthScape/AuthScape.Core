namespace AuthScape.Marketplace.Models
{
    public class MarketplaceProduct
    {
        public List<MarketplaceColumn> Columns { get; set; }
    }

    public class MarketplaceColumn
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public MarketplaceIndexFieldStore FieldStore { get; set; }
    }

    public enum MarketplaceIndexFieldStore
    {
        Yes,
        No
    }
}
