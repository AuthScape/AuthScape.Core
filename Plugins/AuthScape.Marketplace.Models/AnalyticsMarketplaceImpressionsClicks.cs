namespace AuthScape.Marketplace.Models
{
    public class AnalyticsMarketplaceImpressionsClicks
    {
        public Guid Id { get; set; } // session ID for the discovery
        public int Platform { get; set; }
        public string? ProductOrServiceClicked { get; set; }
        public string JSONProductList { get; set; }
        public string JSONFilterSelected { get; set; }
        public long? UserId { get; set; }
        public long? OemCompanyId { get; set; }
        public DateTimeOffset Created { get; set; }
    }
}
