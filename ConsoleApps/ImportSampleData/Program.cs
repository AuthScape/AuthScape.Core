// to import sample data yourself go to: https://www.datablist.com/learn/csv/download-sample-csv-files

using AuthScape.Marketplace.Services;
using CsvHelper;
using ImportSampleData.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using System.Globalization;

Console.WriteLine("Loading...");



var options = new DbContextOptionsBuilder<DatabaseContext>();

options.UseSqlServer("Data Source=localhost;Initial Catalog=AuthScape;Integrated Security=true;Trusted_Connection=true;TrustServerCertificate=true;",
    sqlOptions => sqlOptions.EnableRetryOnFailure());

var context = new DatabaseContext(options.Options);


// Create an AppSettings instance and populate it as needed
var appSettings = new AppSettings
{
    LuceneSearch = new LuceneSearch()
    {
        Container = "",
        StorageConnectionString = ""
    }
};

//// Wrap the AppSettings instance in an Options wrapper
//var optionsAppSettings = Options.Create(appSettings);

//var marketplaceService = new MarketplaceService(context, optionsAppSettings);


//var productCsv = new List<ProductCsv>();

//using (var reader = new StreamReader(@"D:\xfer\testData\products-10000.csv"))
//using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
//{
//    productCsv = csv.GetRecords<ProductCsv>().ToList();
//}

//Console.WriteLine("Generating...");

//var productImports = new List<ProductImport>();
//int total = 10000;
//int index = 1;
//foreach (var productCsvItem in productCsv)
//{
//    Console.WriteLine("Processing ");
//    productImports.Add(new ProductImport()
//    {
//        Availability = productCsvItem.Availability,
//        Brand = productCsvItem.Brand,
//        Category = productCsvItem.Category,
//        Color = productCsvItem.Color,
//        Currency = productCsvItem.Currency,
//        Description = productCsvItem.Description,
//        EAN = productCsvItem.EAN,
//        Index = productCsvItem.Index,
//        Name = productCsvItem.Name,
//        Price = productCsvItem.Price,
//        Size = productCsvItem.Size,
//        Stock = productCsvItem.Stock,
//        Score = 0,
//    });

//    if (index == total)
//    {
//        //break;
//    }

//    index++;
//}
//await marketplaceService.GenerateMLModel(productImports);

Console.WriteLine("Completed...");