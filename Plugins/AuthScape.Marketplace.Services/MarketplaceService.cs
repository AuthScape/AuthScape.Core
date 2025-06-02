using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoreBackpack.Time;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Services.Context;
using Services.Database;
using System.Reflection;

namespace AuthScape.Marketplace.Services
{
    public interface IMarketplaceService
    {
        Task<SearchResult2> SearchProducts(SearchParams searchParams);
        Task Clicked(int platformId, string productOrServiceId, long? CompanyId = null);
        Task GenerateMLModel<T>(List<T> documents, int platformId = 0, long? privateLabelCompanyId = null, string? cachePath = null) where T : new();
        Task RemoveAllFilesInFolder(string containerName, string folderName);
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
            searchParams.Container = appSettings.LuceneSearch.Container;
            searchParams.StorageConnectionString = appSettings.LuceneSearch.StorageConnectionString;

            var containerLocation = searchParams.Container;
            if (searchParams.OemCompanyId != null)
            {
                containerLocation += "/" + searchParams.OemCompanyId;
            }
            else
            {
                containerLocation += "/" + "0";
            }

            containerLocation += "/" + searchParams.PlatformId;


            string cachePath = "";
            // 1. Define persistent cache directory
            if (searchParams.OemCompanyId != null)
            {
                cachePath = Path.Combine(
                    Environment.GetEnvironmentVariable("HOME") ?? string.Empty,
                    "cache", "LuceneCache", searchParams.OemCompanyId.Value.ToString(), searchParams.PlatformId.ToString()
                );
            }
            else
            {
                cachePath = Path.Combine(
                    Environment.GetEnvironmentVariable("HOME") ?? string.Empty,
                    "cache", "LuceneCache", "0", searchParams.PlatformId.ToString()
                );
            }


            if (!System.IO.Directory.Exists(cachePath))
            {
                System.IO.Directory.CreateDirectory(cachePath);
            }

            using (var cacheDirectory = new SimpleFSDirectory(new DirectoryInfo(cachePath)))
            {
                using (AzureDirectory azureDirectory = new AzureDirectory(searchParams.StorageConnectionString, containerLocation, cacheDirectory))
                {

                    using (var reader = DirectoryReader.Open(azureDirectory))
                    {
                        var searcher = new IndexSearcher(reader);

                        var booleanQuery = new BooleanQuery();
                        var hasFilters = false;

                        // Filters
                        if (searchParams.SearchParamFilters != null && searchParams.SearchParamFilters.Any())
                        {
                            // Group filters by their category
                            var groupedFilters = searchParams.SearchParamFilters.GroupBy(f => f.Category);

                            foreach (var group in groupedFilters)
                            {
                                // Create a subquery for each category with OR between options
                                var categoryQuery = new BooleanQuery();
                                foreach (var filter in group)
                                {
                                    if (!String.IsNullOrWhiteSpace(filter.Subcategory))
                                    {
                                        //categoryQuery.Add(new TermQuery(new Term(filter.Category, filter.Subcategory)), Occur.SHOULD);

                                        var childCategory = await databaseContext.ProductCardCategories
                                            .AsNoTracking()
                                            .Where(z => z.ParentName == filter.Category && z.PlatformId == searchParams.PlatformId)
                                            .FirstOrDefaultAsync();

                                        if (childCategory != null)
                                        {
                                            categoryQuery.Add(new TermQuery(new Term(childCategory.Name, filter.Option)), Occur.SHOULD);
                                        }
                                    }
                                    else
                                    {
                                        categoryQuery.Add(new TermQuery(new Term(filter.Category, filter.Option)), Occur.SHOULD);
                                    }
                                }

                                // Add the category subquery as a MUST clause to the main query (AND between categories)
                                booleanQuery.Add(categoryQuery, Occur.MUST);
                                hasFilters = true;
                            }
                        }


                        Sort sortCriteria = new Sort(new SortField("Score", SortFieldType.INT64, true));


                        ScoreDoc[] showAllPossibleHits = searcher.Search(new MatchAllDocsQuery(), int.MaxValue, sortCriteria).ScoreDocs;

                        ScoreDoc[] filteredOutHits;
                        if (!hasFilters)
                        {
                            filteredOutHits = showAllPossibleHits;
                        }
                        else
                        {
                            filteredOutHits = searcher.Search(booleanQuery, int.MaxValue, sortCriteria).ScoreDocs;
                        }

                        var start = (searchParams.PageNumber - 1) * searchParams.PageSize;
                        var hits = filteredOutHits.Skip(start).Take(searchParams.PageSize).ToList();

                        var listOfReferenceIds = new List<string>();

                        var records = new List<List<ProductResult>>();
                        foreach (var hit in hits)
                        {
                            var doc = searcher.Doc(hit.Doc);
                            var record = new List<ProductResult>();
                            foreach (var field in doc.Fields)
                            {
                                var isArray = await databaseContext.ProductCardCategories
                                    .AsNoTracking()
                                    .Where(z => z.Name == field.Name && z.CompanyId == searchParams.OemCompanyId && z.PlatformId == searchParams.PlatformId)
                                    .Select(z => z.IsArray)
                                    .FirstOrDefaultAsync();
                                if (field.Name == "Assets")
                                {

                                }
                                if (isArray)
                                {
                                    var catValues = doc.GetValues(field.Name);
                                    foreach (var catValue in catValues)
                                    {
                                        if (!record.Where(z => z.Name == field.Name && z.Value == catValue).Any())
                                        {
                                            record.Add(new ProductResult { Name = field.Name, Value = catValue });
                                        }
                                    }
                                }
                                else
                                {
                                    var catValue = doc.Get(field.Name);

                                    record.Add(new ProductResult { Name = field.Name, Value = catValue });

                                    if (field.Name == "Id")
                                    {
                                        listOfReferenceIds.Add(doc.Get(field.Name));
                                    }
                                }

                            }
                            records.Add(record);
                        }

                        var filters = await GetAvailableFilters(searchParams, searcher, filteredOutHits, showAllPossibleHits, booleanQuery, searchParams.PlatformId, searchParams.OemCompanyId);

                        int totalPages = (int)Math.Ceiling((double)filteredOutHits.Length / searchParams.PageSize);


                        var cateoryFilter = new List<FilterTracking>();
                        foreach (var filter in filters)
                        {
                            foreach (var option in filter.Options.Where(c => c.IsChecked))
                            {
                                cateoryFilter.Add(new FilterTracking()
                                {
                                    Category = filter.Category,
                                    Subcategory = "", // need to fix this...
                                    Option = option.Name
                                });
                            }
                        }


                        // track the impressions
                        var impressionTracking = new AnalyticsMarketplaceImpressionsClicks()
                        {
                            Platform = searchParams.PlatformId,

                            JSONProductList = JsonConvert.SerializeObject(listOfReferenceIds),
                            ProductOrServiceClicked = null,
                            JSONFilterSelected = JsonConvert.SerializeObject(cateoryFilter),
                            UserId = searchParams.UserId,
                            OemCompanyId = searchParams.OemCompanyId,
                            Created = SystemTime.Now
                        };
                        await databaseContext.AnalyticsMarketplaceImpressionsClicks.AddRangeAsync(impressionTracking);
                        await databaseContext.SaveChangesAsync();


                        return new SearchResult2
                        {
                            Products = records,
                            Filters = filters,
                            PageNumber = searchParams.PageNumber,
                            PageSize = totalPages,
                            Total = filteredOutHits.Length,
                            TrackingId = impressionTracking.Id
                        };
                    }
                }
            }
        }

        async Task<IEnumerable<CategoryFilters>> GetAvailableFilters(
            SearchParams searchParams,
            IndexSearcher searcher,
            ScoreDoc[] filteredOutHits,
            ScoreDoc[] showAllPossibleHits,
            BooleanQuery booleanQuery,
            int platformId = 1,
            long? companyId = null)
        {
            var categoryOptions = new HashSet<CategoryFilters>();
            var activeFilters = searchParams.SearchParamFilters?.ToList() ?? new List<SearchParamFilter>();

            // Collect all unique category names from the index
            var allCategories = new HashSet<string>();
            foreach (var hit in showAllPossibleHits)
            {
                var doc = searcher.Doc(hit.Doc);
                foreach (var field in doc.Fields)
                {
                    if (field.Name == "Id" || field.Name == "Name" || field.Name == "ReferenceId" || field.Name == "Score") continue;
                    allCategories.Add(field.Name);
                }
            }

            foreach (var category in allCategories)
            {
                // Database check for category visibility
                var productCardCategory = await databaseContext.ProductCardCategories
                    .Where(p => p.CompanyId == companyId && p.PlatformId == platformId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == category.ToLower());

                // Skip categories marked as None
                if (productCardCategory != null && productCardCategory.ProductCardCategoryType == ProductCardCategoryType.None)
                    continue;

                // Build query with other active filters (excluding the current category)
                var otherFilters = activeFilters
                    .Where(f => !f.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Query otherFiltersQuery = await BuildOtherFiltersQuery(otherFilters, databaseContext, platformId);

                // Get hits matching other filters
                var otherHits = searcher.Search(otherFiltersQuery, int.MaxValue).ScoreDocs;

                // Collect options from these hits
                var filterOptions = new List<FilterOption>();

                // Check if the category is a parent category by looking for a related record in the database
                var parentCategory = await databaseContext.ProductCardCategories
                    .AsNoTracking()
                    .Where(p => p.ParentName == category && p.PlatformId == platformId)
                    .FirstOrDefaultAsync();

                foreach (var hit in otherHits)
                {
                    var doc = searcher.Doc(hit.Doc);
                    var value = doc.Get(category);

                    if (value != null)
                    {
                        var subcategories = new List<FilterOption>();

                        // If the category has subcategories (parent exists)
                        if (parentCategory != null)
                        {
                            // Use showAllPossibleHits (the full set) to calculate subcategory counts
                            // New code: using filtered hits only
                            // Instead of iterating over showAllPossibleHits, iterate over otherHits 
                            // (which apply the filters from other categories)
                            foreach (var docHit in otherHits)
                            {
                                var doc2 = searcher.Doc(docHit.Doc);
                                // Assuming the field "Category" holds the subcategory value
                                var subCat = doc2.Get(parentCategory.Name);
                                // And "ParentCategory" holds the name of the parent option for the subcategory
                                var parentCat = doc2.Get(parentCategory.ParentName);

                                if (!string.IsNullOrWhiteSpace(subCat) &&
                                    !string.IsNullOrWhiteSpace(parentCat) &&
                                    parentCat.Equals(value, StringComparison.OrdinalIgnoreCase))
                                {
                                    var existingSubCat = subcategories.FirstOrDefault(z =>
                                        z.Key.Equals(subCat, StringComparison.OrdinalIgnoreCase));
                                    if (existingSubCat == null)
                                    {
                                        subcategories.Add(new FilterOption()
                                        {
                                            Key = subCat,
                                            Value = 1
                                        });
                                    }
                                    else
                                    {
                                        existingSubCat.Value++;
                                    }
                                }
                            }



                            // Only add this parent option if there are subcategories available
                            if (subcategories.Any())
                            {
                                var filterOption = filterOptions.FirstOrDefault(f => f.Key == value);
                                if (filterOption == null)
                                {
                                    filterOptions.Add(new FilterOption()
                                    {
                                        Key = value,
                                        Value = 1,
                                        Subcategories = subcategories
                                    });
                                }
                            }
                        }
                        else
                        {
                            // If no subcategories are present, add the category directly
                            var filterOption = filterOptions.FirstOrDefault(f => f.Key == value);
                            if (filterOption == null)
                            {
                                filterOptions.Add(new FilterOption()
                                {
                                    Key = value,
                                    Value = 1,
                                    Subcategories = subcategories
                                });
                            }
                        }
                    }
                }

                // Only add category if it has options
                if (filterOptions.Count > 0)
                {
                    var categoryFilter = new CategoryFilters
                    {
                        Category = category,
                        Options = filterOptions.Select(opt => new CategoryFilterOption
                        {
                            Name = opt.Key,
                            IsChecked = activeFilters.Any(f =>
                                f.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                                f.Option.Equals(opt.Key, StringComparison.OrdinalIgnoreCase)),
                            Count = opt.Value,
                            Subcategories = opt.Subcategories != null ? opt.Subcategories.Select(z => new CategoryFilterOption()
                            {
                                Name = z.Key,
                                Count = z.Value,
                                IsChecked = false
                            }).OrderBy(z => z.Name) : null
                        }).OrderBy(z => z.Name)
                    };

                    categoryOptions.Add(categoryFilter);
                }
            }

            return categoryOptions.OrderBy(z => z.Category);
        }

        public async Task<Query> BuildOtherFiltersQuery(
            List<SearchParamFilter> otherFilters,
            DatabaseContext databaseContext, int platformId) // Add database context
        {
            if (!otherFilters.Any())
                return new MatchAllDocsQuery();

            var booleanQuery = new BooleanQuery();

            // Group filters by their original category
            var groupedFilters = otherFilters.GroupBy(f => f.Category);

            foreach (var group in groupedFilters)
            {
                string originalCategory = group.Key;
                string resolvedCategory = originalCategory;

                // Check if any filter in this group has a subcategory
                bool hasSubcategory = group.Any(f => !string.IsNullOrWhiteSpace(f.Subcategory));

                // If subcategory exists, resolve the child category from the database
                if (hasSubcategory)
                {
                    var childCategory = await databaseContext.ProductCardCategories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(z => z.ParentName == originalCategory && z.PlatformId == platformId);

                    if (childCategory != null)
                        resolvedCategory = childCategory.Name;
                }

                // Build the category query with the resolved category name
                var categoryQuery = new BooleanQuery();
                foreach (var filter in group)
                {
                    categoryQuery.Add(
                        new TermQuery(new Term(resolvedCategory, filter.Option)),
                        Occur.SHOULD
                    );
                }

                booleanQuery.Add(categoryQuery, Occur.MUST);
            }

            return booleanQuery;
        }

        public async Task Clicked(int platformId, string productOrServiceId, long? CompanyId = null)
        {
            var analytics = await databaseContext.AnalyticsMarketplaceImpressionsClicks
                .Where(s => s.Platform == platformId && s.OemCompanyId == CompanyId)
                .FirstOrDefaultAsync();

            if (analytics != null)
            {
                analytics.ProductOrServiceClicked = productOrServiceId;
                await databaseContext.SaveChangesAsync();
            }
        }

        public async Task GenerateMLModel<T>(List<T> documents, int platformId = 1, long? privateLabelCompanyId = null, string? cachePath = null) where T : new()
        {
            var containerLocation = appSettings.LuceneSearch.Container;
            if (privateLabelCompanyId != null)
            {
                containerLocation += "/" + privateLabelCompanyId;
            }
            else
            {
                containerLocation += "/" + "0";
            }

            containerLocation += "/" + platformId;


            var _document = documents.FirstOrDefault();
            if (_document == null)
            {
                return;
            }

            #region Clear the Product Card Categories

            var productCardCategoies = databaseContext.ProductCardCategories
                .Where(z => z.CompanyId == privateLabelCompanyId && z.PlatformId == platformId);

            databaseContext.ProductCardCategories.RemoveRange(productCardCategoies);
            await databaseContext.SaveChangesAsync();

            #endregion

            var properties = _document.GetType().GetProperties();
            foreach (var field in properties)
            {
                bool isArray = false;
                if (field.PropertyType == typeof(IEnumerable<string>) || field.PropertyType == typeof(List<string>))
                {
                    isArray = true;
                }

                // we need to figure out the parent still....
                var category = (MarketplaceIndex)System.Attribute.GetCustomAttribute(field, typeof(MarketplaceIndex));

                await databaseContext.ProductCardCategories.AddAsync(new AuthScape.Marketplace.Models.ProductCardCategory()
                {
                    Name = category.CategoryName != null ? category.CategoryName : field.Name,
                    CompanyId = privateLabelCompanyId,
                    PlatformId = platformId,
                    ParentName = category != null ? category.ParentCategory : null,
                    IsArray = isArray,
                    ProductCardCategoryType = category.ProductCardCategoryType,
                });
            }
            await databaseContext.SaveChangesAsync();


            // 1. Configure AzureDirectory with proper lock handling
            using (AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, containerLocation, null))
            {
                // 2. Ensure index is unlocked (critical for Azure blob storage)
                if (IndexWriter.IsLocked(azureDirectory))
                {
                    IndexWriter.Unlock(azureDirectory);
                }


                //azureDirectory.UseSimpleFSDirectory = true; // Disable MMap
                //var directory = FSDirectory.Open("path_to_index");
                var analyzer = new StandardAnalyzer(luceneVersion);
                var config = new IndexWriterConfig(luceneVersion, analyzer)
                {
                    OpenMode = OpenMode.CREATE,
                };
                using (var writer = new IndexWriter(azureDirectory, config))
                {
                    var luceneDocuments = new List<Lucene.Net.Documents.Document>();
                    foreach (var space in documents)
                    {
                        var doc = new Lucene.Net.Documents.Document();

                        foreach (var field in properties)
                        {
                            bool isArray = false;
                            if (field.PropertyType == typeof(IEnumerable<string>) || field.PropertyType == typeof(List<string>))
                            {
                                isArray = true;
                            }

                            var value = field.GetValue(space);
                            if (value == null)
                            {
                                continue;
                            }

                            if (isArray) // list array
                            {
                                foreach (var property in (List<string>)value)
                                {
                                    AddField(field, property, doc);
                                }
                            }
                            else // single value
                            {
                                AddField(field, field.GetValue(space), doc);
                            }
                        }

                        writer.AddDocument(doc);
                    }

                    writer.Commit();
                    writer.WaitForMerges();
                }
            }

            ClearLocalCache(platformId, privateLabelCompanyId, cachePath);
        }

        private void ClearLocalCache(int platformId = 0, long? privateLabelCompanyId = null, string? cachePath = null)
        {
            string _cachePath = "";
            // 1. Define persistent cache directory
            if (privateLabelCompanyId != null)
            {
                _cachePath = Path.Combine(
                    Environment.GetEnvironmentVariable("HOME") ?? string.Empty,
                    "cache", "LuceneCache", privateLabelCompanyId.Value.ToString(), platformId.ToString()
                );
            }
            else
            {
                _cachePath = Path.Combine(
                    Environment.GetEnvironmentVariable("HOME") ?? string.Empty,
                    "cache", "LuceneCache", "0", platformId.ToString()
                );
            }


            if (!String.IsNullOrWhiteSpace(cachePath))
            {
                _cachePath = cachePath + _cachePath;
            }


            // now that we have the cashed path, I need to remove the files...
            if (System.IO.Directory.Exists(_cachePath))
            {
                foreach (string file in System.IO.Directory.GetFiles(_cachePath))
                {
                    File.Delete(file);
                }

                foreach (string dir in System.IO.Directory.GetDirectories(_cachePath))
                {
                    System.IO.Directory.Delete(dir, true);
                }
            }
        }

        private void AddField(PropertyInfo field, object value, Lucene.Net.Documents.Document doc)
        {
            var typeOfField = (MarketplaceIndex)System.Attribute.GetCustomAttribute(field, typeof(MarketplaceIndex));


            var category = (MarketplaceIndex)System.Attribute.GetCustomAttribute(field, typeof(MarketplaceIndex));
            var fieldName = category.CategoryName != null ? category.CategoryName : field.Name;


            switch (typeOfField.ProductCardCategoryType)
            {
                case ProductCardCategoryType.None:
                    doc.Fields.Add(new StringField(fieldName, value.ToString(), Field.Store.YES));
                    break;
                case ProductCardCategoryType.BinaryField:
                    doc.Fields.Add(new StringField(fieldName, value.ToString(), Field.Store.YES));
                    break;
                case ProductCardCategoryType.Int64Field:
                    doc.Fields.Add(new Int64Field(fieldName, Convert.ToInt64(value), Field.Store.YES));
                    break;
                case ProductCardCategoryType.Int32Field:
                    doc.Fields.Add(new Int32Field(fieldName, Convert.ToInt32(value), Field.Store.YES));
                    break;
                case ProductCardCategoryType.StoredField:
                    break;
                case ProductCardCategoryType.StringField:
                    doc.Fields.Add(new StringField(fieldName, value.ToString(), Field.Store.YES));
                    break;
                case ProductCardCategoryType.SingleField:
                    doc.Fields.Add(new SingleField(fieldName, float.Parse(value.ToString()), Field.Store.YES));
                    break;
                case ProductCardCategoryType.TextField:
                    doc.Fields.Add(new TextField(fieldName, value.ToString(), Field.Store.YES));
                    break;
                case ProductCardCategoryType.DoubleField:
                    doc.Fields.Add(new DoubleField(fieldName, Double.Parse(value.ToString()), Field.Store.YES));
                    break;
                //case ProductCardCategoryType.SortedSetDocValuesField:
                //    break;
                //case ProductCardCategoryType.SortedDocValuesField:
                //    break;
                default:
                    doc.Fields.Add(new StringField(fieldName, value.ToString(), Field.Store.YES));
                    break;
            }
        }

        public async Task RemoveAllFilesInFolder(string containerName, string folderName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(appSettings.LuceneSearch.StorageConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: folderName))
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync();
                Console.WriteLine($"Deleted: {blobItem.Name}");

            }
        }
    }
}