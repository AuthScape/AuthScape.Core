using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;
using AuthScape.Models;
using AuthScape.Models.Exceptions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoreBackpack.Azure;
using CoreBackpack.Time;
using CoreBackpack.URL;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Services.Context;
using Services.Database;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace AuthScape.Marketplace.Services
{
    public interface IMarketplaceService
    {
        Task<SearchResult2> SearchProducts(SearchParams searchParams);
        Task Clicked(int platformId, string productOrServiceId, long? CompanyId = null);
        Task GenerateMLModel<T>(List<T> documents, int platformId = 1, long? privateLabelCompanyId = null, string? cachePath = null) where T : new();
        Task RemoveAllFilesInFolder(string containerName, string folderName);
    }

    public class MarketplaceService : IMarketplaceService
    {
        readonly AppSettings appSettings;
        readonly DatabaseContext databaseContext;
        readonly LuceneVersion luceneVersion;
        readonly IBlobStorage blobStorage;
        public MarketplaceService(DatabaseContext databaseContext, IOptions<AppSettings> appSettings, IBlobStorage blobStorage)
        {
            this.databaseContext = databaseContext;
            this.blobStorage = blobStorage;

            this.appSettings = appSettings.Value;
            luceneVersion = LuceneVersion.LUCENE_48;
        }
        public async Task<SearchResult2> SearchProducts(SearchParams searchParams)
        {
            if (!String.IsNullOrEmpty(searchParams.TextSearch))
            {
                searchParams.TextSearch = searchParams.TextSearch.ToLower();
            }

            searchParams.Container = appSettings.LuceneSearch.Container;
            searchParams.StorageConnectionString = appSettings.LuceneSearch.StorageConnectionString;

            var containerLocation = searchParams.Container;
            containerLocation += "/" + (searchParams.OemCompanyId?.ToString() ?? "0");
            containerLocation += "/" + searchParams.PlatformId;

            var versionInformation = await GetVersionFile(containerLocation);
            containerLocation += "/" + versionInformation.ToString();


            // fix this to support versioning
            string cachePath = Path.Combine(
                Environment.GetEnvironmentVariable("HOME") ?? string.Empty,
                "cache", "LuceneCache", (searchParams.OemCompanyId?.ToString() ?? "0"), searchParams.PlatformId.ToString(), versionInformation.ToString()
            );


            if (!System.IO.Directory.Exists(cachePath))
                System.IO.Directory.CreateDirectory(cachePath);

            using (var cacheDirectory = new SimpleFSDirectory(new DirectoryInfo(cachePath)))
            using (var azureDirectory = new AzureDirectory(searchParams.StorageConnectionString, containerLocation, cacheDirectory))
            using (var reader = DirectoryReader.Open(azureDirectory))
            {
                var searcher = new IndexSearcher(reader);
                var analyzer = new StandardAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48);
                var BooleanFilterQuery = new BooleanQuery();
                var hasFilters = false;
                var sortCriteria = new Sort(new SortField("Score", SortFieldType.INT64, true));


                var allCats = await databaseContext.ProductCardCategories
                    .AsNoTracking()
                    .Where(c => c.PlatformId == searchParams.PlatformId
                             && c.CompanyId == searchParams.OemCompanyId)
                    .ToListAsync();


                // 🔍 Add text search (if present)
                //if (!string.IsNullOrWhiteSpace(searchParams.TextSearch))
                //{
                //    var textTerms = searchParams.TextSearch
                //        .ToLowerInvariant()
                //        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                //    var fields = new[] { "Name" }; // Adjust based on your indexed fields

                //    var fuzzyTextQuery = new BooleanQuery();

                //    foreach (var field in fields)
                //    {
                //        var fieldQuery = new BooleanQuery();

                //        foreach (var term in textTerms)
                //        {
                //            // Fuzziness: max 2 edits (Levenshtein distance)
                //            var fuzzy = new FuzzyQuery(new Term(field, term), maxEdits: 2);
                //            fieldQuery.Add(fuzzy, Occur.SHOULD); // OR within field
                //        }

                //        fuzzyTextQuery.Add(fieldQuery, Occur.SHOULD); // OR across fields
                //    }

                //    booleanQuery.Add(fuzzyTextQuery, Occur.MUST);
                //    hasFilters = true;
                //}



                // 🧩 Add filter queries

                if (searchParams.SearchParamFilters?.Any() == true)
                {
                    // Group filters by their Category
                    var groupedByCategory = searchParams.SearchParamFilters
                        .GroupBy(f => f.Category, StringComparer.OrdinalIgnoreCase);

                    foreach (var grp in groupedByCategory)
                    {
                        // Within each category: OR together its filters
                        var categoryGroupQuery = new BooleanQuery
                        {
                            MinimumNumberShouldMatch = 1
                        };

                        foreach (var filter in grp)
                        {
                            if (!string.IsNullOrWhiteSpace(filter.Subcategory))
                            {
                                // Both Category AND Categories must match for this option
                                var pairQ = new BooleanQuery
                                {
                                    { new TermQuery(new Term("Category",    filter.Option   )), Occur.MUST },
                                    { new TermQuery(new Term("Categories", filter.Subcategory)), Occur.MUST }
                                };
                                categoryGroupQuery.Add(pairQ, Occur.SHOULD);
                            }
                            else
                            {
                                // Simple term filter
                                categoryGroupQuery.Add(
                                    new TermQuery(new Term(filter.Category, filter.Option)),
                                    Occur.SHOULD);
                            }
                        }

                        // Across different categories: AND each categoryGroupQuery
                        BooleanFilterQuery.Add(categoryGroupQuery, Occur.MUST);
                    }

                    hasFilters = true;
                }

                var booleanQuery = new BooleanQuery {
                    { BooleanFilterQuery, Occur.MUST }    // require at least one of those SHOULDs
                };



                ScoreDoc[] allHits = searcher.Search(new MatchAllDocsQuery(), int.MaxValue, sortCriteria).ScoreDocs;
                ScoreDoc[] filteredHits = hasFilters
                    ? searcher.Search(booleanQuery, int.MaxValue, sortCriteria).ScoreDocs
                    : allHits;

                var start = (searchParams.PageNumber - 1) * searchParams.PageSize;
                var hits = filteredHits.Skip(start).Take(searchParams.PageSize).ToList();

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

                        if (isArray)
                        {
                            foreach (var catValue in doc.GetValues(field.Name))
                            {
                                if (!record.Any(z => z.Name == field.Name && z.Value == catValue))
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
                                listOfReferenceIds.Add(catValue);
                        }
                    }

                    records.Add(record);
                }

                var filters = await GetAvailableFilters(searchParams, searcher, filteredHits, allHits, booleanQuery, searchParams.PlatformId, searchParams.OemCompanyId);
                int totalPages = (int)Math.Ceiling((double)filteredHits.Length / searchParams.PageSize);

                var selectedFilters = filters
                    .SelectMany(f => f.Options.Where(o => o.IsChecked).Select(o => new FilterTracking
                    {
                        Category = f.Category,
                        Subcategory = "", // TBD
                        Option = o.Name
                    }))
                    .ToList();

                var impressionTracking = new AnalyticsMarketplaceImpressionsClicks
                {
                    Platform = searchParams.PlatformId,
                    JSONProductList = JsonConvert.SerializeObject(listOfReferenceIds),
                    ProductOrServiceClicked = null,
                    JSONFilterSelected = JsonConvert.SerializeObject(selectedFilters),
                    UserId = searchParams.UserId,
                    OemCompanyId = searchParams.OemCompanyId,
                    Created = SystemTime.Now
                };

                await databaseContext.AnalyticsMarketplaceImpressionsClicks.AddAsync(impressionTracking);
                await databaseContext.SaveChangesAsync();

                return new SearchResult2
                {
                    Products = records,
                    Filters = filters,
                    PageNumber = searchParams.PageNumber,
                    PageSize = totalPages,
                    Total = filteredHits.Length,
                    TrackingId = impressionTracking.Id
                };
            }
        }

        public async Task<IEnumerable<CategoryFilters>> GetAvailableFilters(
                SearchParams searchParams,
                IndexSearcher searcher,
                ScoreDoc[] filteredOutHits,
                ScoreDoc[] showAllPossibleHits,
                BooleanQuery booleanQuery,
                int platformId = 1,
                long? companyId = null)
        {
            var categoryOptions = new List<CategoryFilters>();
            var activeFilters = searchParams.SearchParamFilters?.ToList()
                                ?? new List<SearchParamFilter>();

            // Get all Lucene field names except system fields
            var fieldInfos = MultiFields.GetMergedFieldInfos(searcher.IndexReader);
            var allCategories = fieldInfos
                .Select(f => f.Name)
                .Where(name => name != "Id" && name != "Name"
                            && name != "ReferenceId" && name != "Score")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Preload all ProductCardCategories
            var allProductCardCategories = await databaseContext.ProductCardCategories
                .Where(p => p.CompanyId == companyId && p.PlatformId == platformId)
                .AsNoTracking()
                .ToListAsync();

            foreach (var category in allCategories)
            {
                // Skip categories of type None or TextField
                var cardCat = allProductCardCategories
                    .FirstOrDefault(p => p.Name.Equals(category, StringComparison.OrdinalIgnoreCase));
                if (cardCat != null &&
                    (cardCat.ProductCardCategoryType == ProductCardCategoryType.None ||
                     cardCat.ProductCardCategoryType == ProductCardCategoryType.TextField))
                {
                    continue;
                }

                // Build a query of all active filters *excluding* this category
                var otherFilters = activeFilters
                    .Where(f => !f.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var otherFiltersQuery = await BuildOtherFiltersQuery(
                    otherFilters, databaseContext, platformId);

                // Fetch all matching docs for those other filters
                var docs = new List<Lucene.Net.Documents.Document>();
                ScoreDoc lastDoc = null;
                while (true)
                {
                    var topDocs = searcher.SearchAfter(lastDoc, otherFiltersQuery, int.MaxValue);
                    if (topDocs.ScoreDocs.Length == 0) break;
                    foreach (var hit in topDocs.ScoreDocs)
                        docs.Add(searcher.Doc(hit.Doc));
                    lastDoc = topDocs.ScoreDocs[^1];
                }

                // Tally option counts
                var filterOptions = new Dictionary<string, FilterOption>(StringComparer.OrdinalIgnoreCase);
                foreach (var doc in docs)
                {
                    foreach (var val in doc.GetValues(category))
                    {
                        if (string.IsNullOrWhiteSpace(val)) continue;
                        if (!filterOptions.TryGetValue(val, out var opt))
                        {
                            opt = new FilterOption { Key = val, Value = 0, Subcategories = new List<FilterOption>() };
                            filterOptions[val] = opt;
                        }
                        opt.Value++;
                    }
                }

                // If this category has subcategories, tally those
                var parentCat = allProductCardCategories
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p.ParentName)
                                      && p.ParentName.Equals(category, StringComparison.OrdinalIgnoreCase));
                if (parentCat != null)
                {
                    var subName = parentCat.Name;
                    var parentName = parentCat.ParentName;
                    var grouped = docs
                        .Select(d => new { Sub = d.Get(subName), Parent = d.Get(parentName) })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Sub) && !string.IsNullOrWhiteSpace(x.Parent))
                        .GroupBy(x => new { x.Parent, x.Sub });
                    foreach (var g in grouped)
                    {
                        var parentRaw = g.Key.Parent;
                        var subRaw = g.Key.Sub;
                        if (filterOptions.TryGetValue(parentRaw, out var pOpt))
                        {
                            var existing = pOpt.Subcategories
                                .FirstOrDefault(s => s.Key.Equals(subRaw, StringComparison.OrdinalIgnoreCase));
                            if (existing == null)
                                pOpt.Subcategories.Add(new FilterOption { Key = subRaw, Value = g.Count() });
                            else
                                existing.Value += g.Count();
                        }
                    }
                }

                // Only include categories that have at least one option
                if (filterOptions.Count > 0)
                {
                    var catFilter = new CategoryFilters
                    {
                        Category = category,
                        Options = filterOptions.Values
                            .OrderBy(o => o.Key)
                            .Select(o => new CategoryFilterOption
                            {
                                Name = o.Key,
                                Count = o.Value,
                                IsChecked = activeFilters.Any(f =>
                                    f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                                    && f.Option.Equals(o.Key, StringComparison.OrdinalIgnoreCase)),
                                Subcategories = o.Subcategories
                                    .OrderBy(s => s.Key)
                                    .Select(s => new CategoryFilterOption
                                    {
                                        Name = s.Key,
                                        Count = s.Value,
                                        IsChecked = false
                                    })
                                    .ToList()
                            })
                            .ToList()
                    };
                    categoryOptions.Add(catFilter);
                }
            }

            return categoryOptions.OrderBy(c => c.Category);
        }





        private async Task<Query> BuildOtherFiltersQuery(
                IEnumerable<SearchParamFilter> filters,
                DatabaseContext databaseContext,
                int platformId)
        {
            if (!filters.Any())
                return new MatchAllDocsQuery();

            var root = new BooleanQuery();

            var grouped = filters
                .GroupBy(f => f.Category, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in grouped)
            {
                var categoryQ = new BooleanQuery { MinimumNumberShouldMatch = 1 };

                foreach (var filter in grp)
                {
                    if (!string.IsNullOrWhiteSpace(filter.Subcategory))
                    {
                        var pairQ = new BooleanQuery
                    {
                        { new TermQuery(new Term("Category",    filter.Option   )), Occur.MUST },
                        { new TermQuery(new Term("Categories", filter.Subcategory)), Occur.MUST }
                    };
                        categoryQ.Add(pairQ, Occur.SHOULD);
                    }
                    else
                    {
                        categoryQ.Add(
                            new TermQuery(new Term(filter.Category, filter.Option)),
                            Occur.SHOULD);
                    }
                }

                root.Add(categoryQ, Occur.MUST);
            }

            return root;
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

        // gets the file version from blob storage
        private async Task<long> GetVersionFile(string containerPath)
        {
            long _version = 0;
            var settings = await databaseContext.Settings.Where(z => z.Name == containerPath).FirstOrDefaultAsync();
            if (settings != null)
            {
                _version = Convert.ToInt64(settings.Value);
            }
            else
            {
                await databaseContext.Settings.AddAsync(new AuthScape.Models.Settings.Settings()
                {
                    Name = containerPath,
                    Value = _version.ToString()
                });
            }

            await databaseContext.SaveChangesAsync();

            return _version;
        }

        // Uploads the version file to blob storage
        private async Task UploadVersionFile(string containerPath, long newVersionNumber)
        {
            var settings = await databaseContext.Settings.Where(z => z.Name == containerPath).FirstOrDefaultAsync();
            if (settings != null)
            {
                settings.Value = newVersionNumber.ToString();
            }
            else
            {
                await databaseContext.Settings.AddAsync(new AuthScape.Models.Settings.Settings()
                {
                    Name = containerPath,
                    Value = newVersionNumber.ToString()
                });
            }

            await databaseContext.SaveChangesAsync();
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

            var versionContainerLocation = containerLocation;

            // we need to know what version we are currently working on. Look in this storage folder for ver.txt
            var versionNumber = await GetVersionFile(versionContainerLocation);

            // increase the version number
            versionNumber++;

            // add the version number to it, so now we have folders with the version within it
            containerLocation += "/" + versionNumber;

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

            await UploadVersionFile(versionContainerLocation, versionNumber);
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