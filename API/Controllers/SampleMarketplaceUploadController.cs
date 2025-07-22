// to import sample data yourself go to: https://www.datablist.com/learn/csv/download-sample-csv-files


using AuthScape.LuceneSearch;
using AuthScape.LuceneSearch.Models;
using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;
using AuthScape.Marketplace.Services;
using CsvHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SampleMarketplaceUploadController : ControllerBase
    {
        readonly IMarketplaceService marketplaceService;
        public SampleMarketplaceUploadController(IMarketplaceService marketplaceService)
        {
            this.marketplaceService = marketplaceService;
        }

        //[HttpGet]
        //public async Task<IActionResult> Get(string input, string field, int totalResults = 10)
        //{
        //    var result = luceneSearchSevice.Search(input, field, totalResults);


        //    foreach (var document in result.Documents)
        //    {

        //        //document.Document.Get("")
        //    }

        //    return Ok();
        //}

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] ProductCatalog catalog)
        {
            var productCsv = new List<ProductCsv>();
            using (var reader = new StreamReader(catalog.File.OpenReadStream()))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                productCsv = csv.GetRecords<ProductCsv>().ToList();
            }

            var productImports = new List<ProductImport>();
            int total = 10000;
            int index = 1;
            foreach (var productCsvItem in productCsv)
            {
                Console.WriteLine("Processing ");
                productImports.Add(new ProductImport()
                {
                    Availability = productCsvItem.Availability,
                    Brand = productCsvItem.Brand,
                    Category = productCsvItem.Category,
                    Color = productCsvItem.Color,
                    Currency = productCsvItem.Currency,
                    Description = productCsvItem.Description,
                    EAN = productCsvItem.EAN,
                    Index = productCsvItem.Index,
                    Name = productCsvItem.Name,
                    Price = productCsvItem.Price,
                    Size = productCsvItem.Size,
                    Stock = productCsvItem.Stock,
                    Score = 0,
                    CardSize = 6
                });

                if (index == total)
                {
                    //break;
                }

                index++;
            }
            await marketplaceService.GenerateMLModel(productImports);


            return Ok();
        }
    }

    public class ProductCatalog
    {
        public IFormFile File { get; set; }
    }

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
