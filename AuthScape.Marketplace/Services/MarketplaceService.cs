using AuthScape.Marketplace.Models;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Services;
using Services.Context;
using Services.Database;
using StrongGrid;
using System.Linq;
using static AuthScape.Marketplace.Services.MarketplaceService;

namespace AuthScape.Marketplace.Services
{
    public interface IMarketplaceService
    {
        Task IndexProducts();
        Task<SearchResult2> SearchProducts(SearchParams searchParams);
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
                foreach (var filter in searchParams.SearchParamFilters)
                {
                    var colorQuery = new BooleanQuery();
                    colorQuery.Add(new TermQuery(new Term(filter.Category, filter.Option)), Occur.SHOULD);
                    booleanQuery.Add(colorQuery, Occur.MUST);
                    hasFilters = true;
                }
            }
            


            var query = hasFilters ? (Query)booleanQuery : new MatchAllDocsQuery();

            var start = (searchParams.PageNumber - 1) * searchParams.PageSize;
            var hits = searcher.Search(query, start + searchParams.PageSize).ScoreDocs.Skip(start).Take(searchParams.PageSize).ToArray();
            var results = hits.Select(hit => searcher.Doc(hit.Doc)).Select(doc => new Product
            {
                Id = Guid.Parse(doc.Get("Id")),
                Name = doc.Get("Name"),
            }).ToList();

            var filters = GetAvailableFilters(searchParams, searcher, hits, booleanQuery);


            var categories = await databaseContext
                .ProductCategories
                .Include(s => s.ProductFields)
                .Select(s => new CategoryResponse()
                {
                    name = s.Name,
                    expanded = true,
                    filters = s.ProductFields.Select(p => new CategoryResponseFilter()
                    {
                        name = p.Name,
                        available = 100
                    })
                })
                .ToListAsync();

            int totalPages = (int)Math.Ceiling((double)results.Count() / searchParams.PageSize);

            return new SearchResult2
            {
                Products = results,
                //Filters = filters,
                Categories = categories,
                PageNumber = searchParams.PageNumber,
                PageSize = totalPages,
                Total = results.Count()
            };
        }

        public class SearchResult2
        {
            public int Total { get; set; }
            public int PageNumber { get; set; }
            public int PageSize { get; set; }
            public List<CategoryResponse> Categories { get; set; }
            public List<Product> Products { get; set; }
            public AvailableFilters Filters { get; set; }
        }

        public class AvailableFilters
        {
            public List<string> Colors { get; set; }
            public List<string> Categories { get; set; }
            public List<string> Sizes { get; set; }
        }

        public HashSet<SearchParamFilter> GetAvailableFilters(SearchParams searchParams, IndexSearcher searcher, ScoreDoc[] hits, BooleanQuery booleanQuery)
        {
            var categoryOptions = new HashSet<SearchParamFilter>();
            //var categorySet = new HashSet<string>();
            //var sizeSet = new HashSet<string>();

            foreach (var hit in hits)
            {
                var doc = searcher.Doc(hit.Doc);

                //foreach (var item in doc)
                //{
                //    var name = item.Name;
                //}


                if (searchParams.SearchParamFilters != null)
                {
                    foreach (var item in searchParams.SearchParamFilters)
                    {
                        var catOption = doc.Get(item.Category);
                        if (catOption != null) categoryOptions.Add(new SearchParamFilter()
                        {
                            Category = item.Category,
                            Option = catOption
                        });
                    }
                }


                
                //var category = doc.Get("Category");
                //var size = doc.Get("Size");

                //if (color != null) colorSet.Add(color);
                //if (category != null) categorySet.Add(category);
                //if (size != null) sizeSet.Add(size);
            }

            return categoryOptions;
        }


        public async Task IndexProducts()
        {
            AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, appSettings.LuceneSearch.Container);

            //var directory = FSDirectory.Open("path_to_index");
            var analyzer = new StandardAnalyzer(luceneVersion);
            var config = new IndexWriterConfig(luceneVersion, analyzer);
            using var writer = new IndexWriter(azureDirectory, config);

            var products = await databaseContext.Products
                .Include(s => s.ProductCategoryFields)
                .ThenInclude(z => z.ProductField)
                .ThenInclude(q => q.ProductCategory)
                .ToListAsync();


            foreach (var product in products)
            {
                var doc = new Lucene.Net.Documents.Document
                {
                    new StringField("Id", product.Id.ToString(), Field.Store.YES),
                    new StringField("Name", product.Name, Field.Store.YES),
                    //new StringField("Price", product.Price.ToString(), Field.Store.YES),
                    //new TextField("Description", product.Description, Field.Store.YES)
                };

                foreach (var field in product.ProductCategoryFields)
                {
                    doc.Fields.Add(new StringField(field.ProductField.ProductCategory.Name, field.ProductField.Name, Field.Store.YES));
                }

                writer.AddDocument(doc);
            }

            writer.Commit();
        }

    }
}
