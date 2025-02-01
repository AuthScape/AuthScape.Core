using AuthScape.Marketplace.Models.Attributes;

namespace AuthScape.Marketplace.Models.CSVReader
{
    public class ProductTest : BaseProductCSVReader
    {
        [TextSearchable]
        public string? Description { get; set; }
        [IndexCategory("Brand Name")]
        public string? BrandName { get; set; }
        public string? MainPhoto { get; set; }
        public string? WebsiteUrl { get; set; }
        [IndexCategory("Category")]
        public string? category1 { get; set; }
        [IndexCategory("Category")]
        public string? category2 { get; set; }
        [IndexCategory("Category")]
        public string? category3 { get; set; }
        public string? ParentCategory { get; set; }
        [ExactSearch]
        public string? PartNumber { get; set; }
    }
}
