using AuthScape.Marketplace.Models.Attributes;

namespace AuthScape.Marketplace.Models.CSVReader
{
    public class BaseProductCSVReader
    {
        public long Id { get; set; }
        [TextSearchable]
        public string Name { get; set; }
    }
}