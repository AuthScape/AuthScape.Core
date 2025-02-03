using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;
using CsvHelper;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using System.Globalization;
using System.Reflection;
using static AuthScape.Marketplace.Services.MarketplaceService;

namespace AuthScape.Marketplace.Services
{
    public interface IMarketplaceService
    {
        Task Generate();
        Task<SearchResult2> SearchProducts(SearchParams searchParams);
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

        public async Task<SearchResult2> SearchProducts(SearchParams searchParams)
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


            //var test = searchParams.LastFilterSelected;


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


            var records = new List<List<ProductResult>>();

            foreach (var doc in hits.Select(hit => searcher.Doc(hit.Doc)))
            {
                var record = new List<ProductResult>();
                foreach (var column in doc)
                {
                    var value = doc.Get(column.Name);
                    record.Add(new ProductResult() { Name = column.Name, Value = value });
                }

                records.Add(record);
            }




            //var hitsAll = searcher.Search(query, 10000;
            var filters = await GetAvailableFilters(searchParams, searcher, hitsAllFilters, booleanQuery);


            int totalPages = (int)Math.Ceiling((double)hitsAll.Length / searchParams.PageSize);

            return new SearchResult2
            {
                Products = records,
                Filters = filters,
                //Categories = categories,
                PageNumber = searchParams.PageNumber,
                PageSize = totalPages,
                Total = hitsAll.Length
            };
        }

        public class SearchResult2
        {
            public int Total { get; set; }
            public int PageNumber { get; set; }
            public int PageSize { get; set; }
            public List<CategoryResponse> Categories { get; set; }
            public List<List<ProductResult>> Products { get; set; }
            public HashSet<CategoryFilters> Filters { get; set; }
        }

        public async Task<HashSet<CategoryFilters>> GetAvailableFilters(SearchParams searchParams, IndexSearcher searcher, ScoreDoc[] hits, BooleanQuery booleanQuery)
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
                if (filter.Key == "Id" || filter.Key == "Name")
                {
                    continue;
                }


                var productCardCategory = await databaseContext.ProductCardCategories
                    .AsNoTracking()
                    .Where(s => s.Name.ToLower() == filter.Key)
                    .FirstOrDefaultAsync();

                if (productCardCategory != null && productCardCategory.ProductCardCategoryType != ProductCardCategoryType.None)
                {

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
            }

            return categoryOptions;
        }



        public async Task Generate()
        {
            AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, appSettings.LuceneSearch.Container);

            //var directory = FSDirectory.Open("path_to_index");
            var analyzer = new StandardAnalyzer(luceneVersion);
            var config = new IndexWriterConfig(luceneVersion, analyzer);
            using var writer = new IndexWriter(azureDirectory, config);


            var products = await databaseContext.ProductCards
                .Include(p => p.ProductCardAndCardFieldMapping)
                .ThenInclude(z => z.ProductField)
                .ThenInclude(z => z.ProductCategory)
                .ToListAsync();

            foreach (var product in products)
            {
                var doc = new Lucene.Net.Documents.Document
                {
                    new StringField("Id", product.Id.ToString(), Field.Store.YES),
                    new StringField("Name", product.Name, Field.Store.YES),
                };

                //if (product.Photo != null)
                //{
                //    doc.Add(new StringField("Photo", product.Photo, Field.Store.YES));
                //}

                foreach (var field in product.ProductCardAndCardFieldMapping)
                {
                    var fieldName = field.ProductField.Name;
                    var categoryName = field.ProductField.ProductCategory.Name;
                    var productCardCategoryType = field.ProductField.ProductCategory.ProductCardCategoryType;

                    if (productCardCategoryType == ProductCardCategoryType.StringField)
                    {
                        doc.Fields.Add(new StringField(categoryName, fieldName, Field.Store.YES));
                    }
                    else if (productCardCategoryType == ProductCardCategoryType.None)
                    {
                        doc.Fields.Add(new StringField(categoryName, fieldName, Field.Store.YES));
                    }
                }

                writer.AddDocument(doc);
            }

            writer.Commit();
        }


        public async Task UploadInventory<T>(Stream stream, int platformType = 1) where T : new()
        {
            databaseContext.ProductCardFields.RemoveRange(await databaseContext.ProductCardFields.ToListAsync());
            databaseContext.ProductCardAndCardFieldMapping.RemoveRange(await databaseContext.ProductCardAndCardFieldMapping.ToListAsync());
            databaseContext.ProductCardCategories.RemoveRange(await databaseContext.ProductCardCategories.ToListAsync());
            databaseContext.ProductCards.RemoveRange(await databaseContext.ProductCards.ToListAsync());
            await databaseContext.SaveChangesAsync();


            var newProducts = new List<T>();

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
                        bool hasValidName = false;

                        T newProduct = new T();

                        foreach (var column in record)
                        {
                            if (column.Key.ToLower() == "name")
                            {
                                if (!String.IsNullOrWhiteSpace(column.Value))
                                {
                                    hasValidName = true;
                                }

                                AssignToProperty<T>(newProduct, "Name", column.Value);
                            }
                            else
                            {
                                // this is a dynamics field that we need to add
                                AssignToProperty<T>(newProduct, column.Key, column.Value);
                            }
                        }

                        if (hasValidName)
                        {
                            newProducts.Add(newProduct);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Error: Unable to read the header record.");
                }
            }



            // start creating the order for the products
            foreach (var newProduct in newProducts)
            {
                var properties = newProduct.GetType().GetProperties();


                // create the product so we have a productId to work with
                var newProductCard = new ProductCard();
                foreach (var property in properties)
                {
                    var value = property.GetValue(newProduct);
                    if (property.Name == "Name")
                    {
                        newProductCard.Name = value.ToString();
                        break;
                    }
                }
                await databaseContext.ProductCards.AddAsync(newProductCard);
                await databaseContext.SaveChangesAsync();


                foreach (var property in properties)
                {
                    var value = property.GetValue(newProduct);
                    var valueAsString = value.ToString();

                    var indexCategory = property.CustomAttributes.Where(a => a.AttributeType == typeof(MarketplaceIndex)).FirstOrDefault();

                    // this is a category index
                    if (indexCategory != null)
                    {
                        var category = (MarketplaceIndex)System.Attribute.GetCustomAttribute(property, typeof(MarketplaceIndex));

                        var categoryName = property.Name;
                        if (!String.IsNullOrWhiteSpace(category.CategoryName))
                        {
                            categoryName = category.CategoryName;
                        }


                        // categories
                        var parentProduct = await databaseContext.ProductCardCategories
                            .AsNoTracking()
                            .Where(p => p.Name.ToLower() == categoryName.ToLower())
                            .FirstOrDefaultAsync();

                        if (parentProduct == null)
                        {
                            parentProduct = new ProductCardCategory()
                            {
                                Name = categoryName,
                                ProductCardCategoryType = category.ProductCardCategoryType
                            };

                            await databaseContext.ProductCardCategories.AddAsync(parentProduct);
                            await databaseContext.SaveChangesAsync();
                        }

                        // this is a option in the category
                        if (!String.IsNullOrWhiteSpace(valueAsString) && valueAsString != "NULL")
                        {
                            var productField1 = await databaseContext.ProductCardFields
                                .AsNoTracking()
                                .Where(p => p.Name.ToLower() == valueAsString.ToLower())
                                .FirstOrDefaultAsync();

                            if (productField1 == null)
                            {
                                productField1 = new ProductCardField()
                                {
                                    ProductCategoryId = parentProduct.Id,
                                    Name = valueAsString,
                                };

                                await databaseContext.ProductCardFields.AddAsync(productField1);
                                await databaseContext.SaveChangesAsync();
                            }


                            // now that we have all the fields defined, let's connect the products to the fields
                            await databaseContext.ProductCardAndCardFieldMapping.AddAsync(new ProductCardAndCardFieldMapping()
                            {
                                ProductFieldId = productField1.Id,
                                ProductId = newProductCard.Id
                            });
                            await databaseContext.SaveChangesAsync();

                        }
                    }
                }
            }
        }


        private void AssignToProperty<T>(object item, string propertyName, object propertyValue) where T : new()
        {
            // Use reflection to set the property value
            PropertyInfo propertyInfo = typeof(T).GetProperty(propertyName);
            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                if (propertyInfo.PropertyType == typeof(string))
                {
                    propertyInfo.SetValue(item, propertyValue);
                }
                else if (propertyInfo.PropertyType == typeof(int))
                {
                    propertyInfo.SetValue(item, int.Parse(propertyValue.ToString()));
                }
                else if (propertyInfo.PropertyType == typeof(long))
                {
                    propertyInfo.SetValue(item, long.Parse(propertyValue.ToString()));
                }
                else if (propertyInfo.PropertyType == typeof(decimal))
                {
                    propertyInfo.SetValue(item, decimal.Parse(propertyValue.ToString()));
                }
                else if (propertyInfo.PropertyType == typeof(bool))
                {
                    propertyInfo.SetValue(item, bool.Parse(propertyValue.ToString()));
                }
                else if (propertyInfo.PropertyType == typeof(DateTime))
                {
                    propertyInfo.SetValue(item, DateTime.Parse(propertyValue.ToString()));
                }
                else
                {
                    // not supported!
                }
            }
        }
    }
}
