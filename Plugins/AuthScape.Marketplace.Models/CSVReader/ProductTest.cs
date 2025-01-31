using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthScape.Marketplace.Models.CSVReader
{
    public class ProductTest
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? BrandName { get; set; }
        public string? MainPhoto { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? category1 { get; set; }
        public string? category2 { get; set; }
        public string? category3 { get; set; }
        public string? ParentCategory { get; set; }
        public string? PartNumber { get; set; }
    }
}
