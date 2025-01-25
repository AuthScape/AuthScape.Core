using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.CSVReader;
using CsvHelper;
using CsvHelper.Configuration;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Services;
using Services.Context;
using Services.Database;
using StrongGrid;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Reflection;
using static AuthScape.Marketplace.Services.MarketplaceService;

namespace AuthScape.Marketplace.Services
{
    public interface IMarketplaceService
    {
        void Generate(List<MarketplaceProduct> marketplaceProducts);
        SearchResult2<T> SearchProducts<T>(SearchParams searchParams) where T : new();
        Task UploadInventory<T>(Stream stream, int platformType = 1) where T : new();
    }

    public class MarketplaceService : IMarketplaceService
    {
        readonly AppSettings appSettings;
        readonly DatabaseContext databaseContext;
        readonly LuceneVersion luceneVersion;
        public MarketplaceService(DatabaseContext databaseContext, IOptions<AppSettings> appSettings)
        {
            this.databaseContext = databaseContext;

            this.appSettings = appSettings.Value;
            luceneVersion = LuceneVersion.LUCENE_48;
        }

        public SearchResult2<T> SearchProducts<T>(SearchParams searchParams) where T : new()
        {
            AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, appSettings.LuceneSearch.Container);
            using var reader = DirectoryReader.Open(azureDirectory);
            var searcher = new IndexSearcher(reader);

            var booleanQuery = new BooleanQuery();
            var hasFilters = false;


            // filters
            if (searchParams.SearchParamFilters != null)
            {
                var colorQuery = new BooleanQuery();
                foreach (var filter in searchParams.SearchParamFilters)
                {
                    hasFilters = true;
                    colorQuery.Add(new TermQuery(new Term(filter.Category, filter.Option)), Occur.SHOULD);
                }

                if (hasFilters)
                {
                    booleanQuery.Add(colorQuery, Occur.MUST);
                }
            }

            ScoreDoc[] hitsAllFilters = null;

            Lucene.Net.Search.MatchAllDocsQuery objMatchAll = new Lucene.Net.Search.MatchAllDocsQuery();
            hitsAllFilters = searcher.Search(objMatchAll, int.MaxValue).ScoreDocs;

            ScoreDoc[] hitsAll = null;
            if (searchParams.SearchParamFilters == null || searchParams.SearchParamFilters.Count() == 0)
            {
                hitsAll = searcher.Search(objMatchAll, int.MaxValue).ScoreDocs;
            }
            else
            {
                hitsAll = searcher.Search(booleanQuery, int.MaxValue).ScoreDocs;
            }


            var test = searchParams.LastFilterSelected;


            //var query = hasFilters ? (Query)booleanQuery : new MatchAllDocsQuery();

            var start = (searchParams.PageNumber - 1) * searchParams.PageSize;

            var hits = hitsAll.Take(start + searchParams.PageSize).Skip(start).ToList();

            //var hits = searcher.Search(query, start + searchParams.PageSize).ScoreDocs.Skip(start).Take(searchParams.PageSize);




            //var results = hits.Select(hit => searcher.Doc(hit.Doc)).Select(doc => new Product
            //{
            //    Id = Guid.Parse(doc.Get("Id")),
            //    Name = doc.Get("Name"),
            //    Photo = doc.Get("Photo")
            //}).ToList();


            hitsAll.Skip(start).Take(searchParams.PageSize).ToList();

            var results = hits.Select(hit => searcher.Doc(hit.Doc)).Select(doc =>
            {
                var obj = new T();
                foreach (var property in typeof(T).GetProperties())
                {
                    var value = doc.Get(property.Name);
                    if (value != null)
                    {
                        if (property.PropertyType == typeof(Guid))
                        {
                            property.SetValue(obj, Guid.Parse(value));
                        }
                        else if (property.PropertyType == typeof(string))
                        {
                            property.SetValue(obj, value);
                        }
                        else if (property.PropertyType == typeof(int))
                        {
                            property.SetValue(obj, int.Parse(value));
                        }
                        else if (property.PropertyType == typeof(decimal))
                        {
                            property.SetValue(obj, decimal.Parse(value));
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            property.SetValue(obj, bool.Parse(value));
                        }
                        else if (property.PropertyType == typeof(DateTime))
                        {
                            property.SetValue(obj, DateTime.Parse(value));
                        }
                    }
                }
                return obj;
            }).ToList();




            //var hitsAll = searcher.Search(query, 10000;
            var filters = GetAvailableFilters(searchParams, searcher, hitsAllFilters, booleanQuery);


            int totalPages = (int)Math.Ceiling((double)hitsAll.Length / searchParams.PageSize);

            return new SearchResult2<T>
            {
                Products = results,
                Filters = filters,
                //Categories = categories,
                PageNumber = searchParams.PageNumber,
                PageSize = totalPages,
                Total = hitsAll.Length
            };
        }

        public class SearchResult2<T>
        {
            public int Total { get; set; }
            public int PageNumber { get; set; }
            public int PageSize { get; set; }
            public List<CategoryResponse> Categories { get; set; }
            public List<T> Products { get; set; }
            public HashSet<CategoryFilters> Filters { get; set; }
        }

        public HashSet<CategoryFilters> GetAvailableFilters(SearchParams searchParams, IndexSearcher searcher, ScoreDoc[] hits, BooleanQuery booleanQuery)
        {
            var categoryOptions = new HashSet<CategoryFilters>();


            // Collect available filters
            var availableFilters = new Dictionary<string, HashSet<string>>();
            foreach (var hit in hits)
            {
                var doc = searcher.Doc(hit.Doc);
                foreach (var field in doc.Fields)
                {
                    if (!availableFilters.ContainsKey(field.Name))
                    {
                        availableFilters[field.Name] = new HashSet<string>();
                    }
                    availableFilters[field.Name].Add(field.GetStringValue());
                }
            }

            // Convert the dictionary to a more usable format
            var filtersList = availableFilters.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            );

            foreach (var filter in filtersList)
            {
                bool isNew = false;
                if (filter.Key == "Id" || filter.Key == "Name" || filter.Key == "Photo")
                {
                    continue;
                }

                var category = categoryOptions
                    .Where(s => s.Category == filter.Key)
                    .FirstOrDefault();


                if (category != null)
                {
                    if (category.Options == null)
                    {
                        category.Category = filter.Key;
                        category.Options = new List<string>();
                    }
                }
                else
                {
                    isNew = true;

                    category = new CategoryFilters();
                    category.Category = filter.Key;
                    category.Options = new List<string>();
                }


                foreach (var option in filter.Value)
                {
                    //var optionItem = category.Options.Where(a => a == option).FirstOrDefault();
                    //if (optionItem == null)
                    //{
                        category.Options.Add(option);
                    //}
                }


                if (isNew)
                {
                    categoryOptions.Add(category);
                }
            }

            return categoryOptions;
        }



        public void Generate(List<MarketplaceProduct> marketplaceProducts)
        {
            AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, appSettings.LuceneSearch.Container);

            //var directory = FSDirectory.Open("path_to_index");
            var analyzer = new StandardAnalyzer(luceneVersion);
            var config = new IndexWriterConfig(luceneVersion, analyzer);
            using var writer = new IndexWriter(azureDirectory, config);

            foreach (var marketplaceProduct in marketplaceProducts)
            {
                var doc = new Lucene.Net.Documents.Document();
                foreach (var column in marketplaceProduct.Columns)
                {
                    doc.Add(new StringField(column.Name, column.Value, column.FieldStore == MarketplaceIndexFieldStore.Yes ? Field.Store.YES : Field.Store.NO));
                }
                writer.AddDocument(doc);
            }





            //var products = await databaseContext.Products
            //    .Include(s => s.ProductCategoryFields)
            //    .ThenInclude(z => z.ProductField)
            //    .ThenInclude(q => q.ProductCategory)
            //    .ToListAsync();

            //foreach (var product in products)
            //{
            //    var doc = new Lucene.Net.Documents.Document
            //    {
            //        new StringField("Id", product.Id.ToString(), Field.Store.YES),
            //        new StringField("Name", product.Name, Field.Store.YES),
            //        //new StringField("Price", product.Price.ToString(), Field.Store.YES),
            //        //new TextField("Description", product.Description, Field.Store.YES)
            //    };

            //    if (product.Photo != null)
            //    {
            //        doc.Add(new StringField("Photo", product.Photo, Field.Store.YES));
            //    }

            //    foreach (var field in product.ProductCategoryFields)
            //    {
            //        doc.Fields.Add(new StringField(field.ProductField.ProductCategory.Name, field.ProductField.Name, Field.Store.YES));
            //    }

            //    writer.AddDocument(doc);
            //}

            writer.Commit();
        }


        public async Task UploadInventory<T>(Stream stream, int platformType = 1) where T : new()
        {
            databaseContext.ProductFields.RemoveRange(await databaseContext.ProductFields.ToListAsync());
            databaseContext.ProductCategoryFields.RemoveRange(await databaseContext.ProductCategoryFields.ToListAsync());
            databaseContext.ProductCategories.RemoveRange(await databaseContext.ProductCategories.ToListAsync());
            databaseContext.Products.RemoveRange(await databaseContext.Products.ToListAsync());
            await databaseContext.SaveChangesAsync();








            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                if (csv.Read() && csv.ReadHeader()) // Ensure reading of header
                {
                    var records = new List<Dictionary<string, string>>();

                    while (csv.Read())
                    {
                        var record = new Dictionary<string, string>();
                        foreach (var header in csv.HeaderRecord)
                        {
                            record[header] = csv.GetField(header);
                        }
                        records.Add(record);
                    }


                    foreach (var record in records)
                    {
                        var newProduct = new Product();

                        foreach (var column in record)
                        {
                            if (column.Key.ToLower() == "Name")
                            {
                                newProduct.Name = column.Value;
                            }
                            else
                            {

                            }
                        }
                    }

                    await databaseContext.Products.AddAsync(newProduct);
                    await databaseContext.SaveChangesAsync();
                }
                else
                {
                    Console.WriteLine("Error: Unable to read the header record.");
                }












                    //var records = csv.GetRecords<T>().ToList();
                    //foreach (var record in records)
                    //{
                    //    ProductCategory? parentProduct = null;

                    //    ProductField? productField1 = null;
                    //    ProductField? productField2 = null;
                    //    ProductField? productField3 = null;


                    //    // categories
                    //    if (!String.IsNullOrWhiteSpace(record.ParentCategory) && record.ParentCategory != "NULL")
                    //    {
                    //        parentProduct = await databaseContext.ProductCategories
                    //            .AsNoTracking()
                    //            .Where(p => p.Name.ToLower() == record.ParentCategory.ToLower())
                    //            .FirstOrDefaultAsync();

                    //        if (parentProduct == null)
                    //        {
                    //            parentProduct = new ProductCategory()
                    //            {
                    //                Name = record.ParentCategory
                    //            };

                    //            await databaseContext.ProductCategories.AddAsync(parentProduct);
                    //            await databaseContext.SaveChangesAsync();
                    //        }
                    //    }



                    //    // options AKA fields
                    //    if (!String.IsNullOrWhiteSpace(record.category1) && record.category1 != "NULL")
                    //    {
                    //        productField1 = await databaseContext.ProductFields
                    //            .AsNoTracking()
                    //            .Where(p => p.Name.ToLower() == record.category1.ToLower())
                    //            .FirstOrDefaultAsync();

                    //        if (productField1 == null)
                    //        {
                    //            productField1 = new ProductField()
                    //            {
                    //                ProductCategoryId = parentProduct.Id,
                    //                Name = record.category1
                    //            };

                    //            await databaseContext.ProductFields.AddAsync(productField1);
                    //            await databaseContext.SaveChangesAsync();
                    //        }
                    //    }

                    //    if (!String.IsNullOrWhiteSpace(record.category2) && record.category2 != "NULL")
                    //    {
                    //        productField2 = await databaseContext.ProductFields
                    //            .AsNoTracking()
                    //            .Where(p => p.Name.ToLower() == record.category2.ToLower())
                    //            .FirstOrDefaultAsync();

                    //        if (productField2 == null)
                    //        {
                    //            productField2 = new ProductField()
                    //            {
                    //                ProductCategoryId = parentProduct.Id,
                    //                Name = record.category2
                    //            };

                    //            await databaseContext.ProductFields.AddAsync(productField2);
                    //            await databaseContext.SaveChangesAsync();
                    //        }
                    //    }

                    //    if (!String.IsNullOrWhiteSpace(record.category3) && record.category3 != "NULL")
                    //    {
                    //        productField3 = await databaseContext.ProductFields
                    //            .AsNoTracking()
                    //            .Where(p => p.Name.ToLower() == record.category3.ToLower())
                    //            .FirstOrDefaultAsync();

                    //        if (productField3 == null)
                    //        {
                    //            productField3 = new ProductField()
                    //            {
                    //                ProductCategoryId = parentProduct.Id,
                    //                Name = record.category3
                    //            };

                    //            await databaseContext.ProductFields.AddAsync(productField3);
                    //            await databaseContext.SaveChangesAsync();
                    //        }
                    //    }





                    //    var newProduct = new Product();
                    //    newProduct.Name = record.Name;
                    //    newProduct.Photo = record.MainPhoto;

                    //    await databaseContext.Products.AddAsync(newProduct);
                    //    await databaseContext.SaveChangesAsync();



                    //    if (productField1 != null)
                    //    {
                    //        await databaseContext.ProductCategoryFields.AddAsync(new ProductCategoryField()
                    //        {
                    //            ProductId = newProduct.Id,
                    //            ProductFieldId = productField1.Id
                    //        });
                    //    }

                    //    if (productField2 != null)
                    //    {
                    //        await databaseContext.ProductCategoryFields.AddAsync(new ProductCategoryField()
                    //        {
                    //            ProductId = newProduct.Id,
                    //            ProductFieldId = productField2.Id
                    //        });
                    //    }

                    //    if (productField3 != null)
                    //    {
                    //        await databaseContext.ProductCategoryFields.AddAsync(new ProductCategoryField()
                    //        {
                    //            ProductId = newProduct.Id,
                    //            ProductFieldId = productField3.Id
                    //        });
                    //    }










                    //}
                }
            }
    }
}
