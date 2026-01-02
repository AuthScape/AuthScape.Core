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

        /// <summary>
        /// Record a click on a product using the tracking ID from SearchProducts response
        /// </summary>
        Task Clicked(Guid trackingId, string productOrServiceId);

        Task GenerateMLModel<T>(List<T> documents, int platformId = 1, long? privateLabelCompanyId = null, string? cachePath = null) where T : new();
        Task RemoveAllFilesInFolder(string containerName, string folderName);

        /// <summary>
        /// Get paginated/searchable filter options for a specific category (e.g., load more brands)
        /// </summary>
        Task<FilterOptionsResponse> GetFilterOptions(FilterOptionsRequest request);

        /// <summary>
        /// Get analytics data including click-through rates, popular products, and search trends
        /// </summary>
        Task<MarketplaceAnalytics> GetAnalytics(int platformId, long? oemCompanyId = null, int days = 30);
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

            // Pre-load all category metadata ONCE to avoid N+1 queries
            var categoryMetadata = await databaseContext.ProductCardCategories
                .AsNoTracking()
                .Where(c => c.PlatformId == searchParams.PlatformId
                         && c.CompanyId == searchParams.OemCompanyId)
                .ToDictionaryAsync(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            using (var cacheDirectory = new SimpleFSDirectory(new DirectoryInfo(cachePath)))
            using (var azureDirectory = new AzureDirectory(searchParams.StorageConnectionString, containerLocation, cacheDirectory))
            using (var reader = DirectoryReader.Open(azureDirectory))
            {
                var searcher = new IndexSearcher(reader);
                var analyzer = new StandardAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48);
                var BooleanFilterQuery = new BooleanQuery();
                var hasFilters = false;
                var sortCriteria = new Sort(new SortField("Score", SortFieldType.INT64, true));


                // 🔍 Add text search (if present)
                if (!string.IsNullOrWhiteSpace(searchParams.TextSearch))
                {
                    var textTerms = searchParams.TextSearch
                        .ToLowerInvariant()
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    var fields = new[] { "Name", "Description" }; // Search across Name and Description fields

                    var textSearchQuery = new BooleanQuery();

                    foreach (var field in fields)
                    {
                        var fieldQuery = new BooleanQuery();

                        foreach (var term in textTerms)
                        {
                            // Scale fuzziness based on term length to avoid irrelevant matches
                            // Short words need exact or near-exact matches
                            int maxEdits;
                            if (term.Length <= 2)
                            {
                                maxEdits = 0; // Exact match for very short terms
                            }
                            else if (term.Length <= 5)
                            {
                                maxEdits = 1; // Allow 1 typo for short terms (3-5 chars)
                            }
                            else
                            {
                                maxEdits = 2; // Allow 2 typos for longer terms (6+ chars)
                            }

                            if (maxEdits > 0)
                            {
                                var fuzzy = new FuzzyQuery(new Term(field, term), maxEdits: maxEdits);
                                fieldQuery.Add(fuzzy, Occur.SHOULD);
                            }
                            else
                            {
                                // Exact match for very short terms
                                fieldQuery.Add(new TermQuery(new Term(field, term)), Occur.SHOULD);
                            }

                            // Also add prefix query for partial matches (e.g., "fan" matches "fantasy")
                            var prefix = new PrefixQuery(new Term(field, term));
                            fieldQuery.Add(prefix, Occur.SHOULD);
                        }

                        textSearchQuery.Add(fieldQuery, Occur.SHOULD); // OR across fields
                    }

                    BooleanFilterQuery.Add(textSearchQuery, Occur.MUST);
                    hasFilters = true;
                }



                // 💰 Add price range filter
                if (searchParams.MinPrice.HasValue || searchParams.MaxPrice.HasValue)
                {
                    var priceField = searchParams.PriceField ?? "Price";

                    // Convert to double for Lucene range query
                    double minPrice = searchParams.MinPrice.HasValue
                        ? (double)searchParams.MinPrice.Value
                        : double.MinValue;
                    double maxPrice = searchParams.MaxPrice.HasValue
                        ? (double)searchParams.MaxPrice.Value
                        : double.MaxValue;

                    var priceRangeQuery = NumericRangeQuery.NewDoubleRange(
                        priceField,
                        minPrice,
                        maxPrice,
                        minInclusive: true,
                        maxInclusive: true);

                    BooleanFilterQuery.Add(priceRangeQuery, Occur.MUST);
                    hasFilters = true;
                }

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

                // Use efficient counting instead of loading all documents
                Query queryToUse = hasFilters ? booleanQuery : new MatchAllDocsQuery();

                // Get total count efficiently using TotalHits
                var totalHitsResult = searcher.Search(queryToUse, 1);
                int totalCount = totalHitsResult.TotalHits;

                // Get total count for unfiltered (for filter options)
                var allHitsResult = searcher.Search(new MatchAllDocsQuery(), 1);
                int allHitsCount = allHitsResult.TotalHits;

                // Only fetch the documents we need for the current page
                var start = (searchParams.PageNumber - 1) * searchParams.PageSize;
                var docsToFetch = start + searchParams.PageSize;

                // Fetch only what we need with pagination
                var filteredTopDocs = searcher.Search(queryToUse, docsToFetch, sortCriteria);
                var pagedHits = filteredTopDocs.ScoreDocs.Skip(start).Take(searchParams.PageSize).ToList();

                var listOfReferenceIds = new List<string>();
                var records = new List<List<ProductResult>>();

                foreach (var hit in pagedHits)
                {
                    var doc = searcher.Doc(hit.Doc);
                    var record = new List<ProductResult>();

                    foreach (var field in doc.Fields)
                    {
                        // Use pre-loaded dictionary instead of N+1 database queries
                        var isArray = categoryMetadata.TryGetValue(field.Name, out var catMeta) && catMeta.IsArray;

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

                var filters = await GetAvailableFilters(searchParams, searcher, totalCount, allHitsCount, booleanQuery, searchParams.PlatformId, searchParams.OemCompanyId, categoryMetadata);
                int totalPages = (int)Math.Ceiling((double)totalCount / searchParams.PageSize);

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

                // Fire-and-forget analytics tracking to avoid blocking the search response
                var dbConnectionString = appSettings.DatabaseContext;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                            .UseSqlServer(dbConnectionString)
                            .Options);
                        await scope.AnalyticsMarketplaceImpressionsClicks.AddAsync(impressionTracking);
                        await scope.SaveChangesAsync();
                    }
                    catch
                    {
                        // Log error but don't fail the search
                    }
                });

                return new SearchResult2
                {
                    Products = records,
                    Filters = filters,
                    PageNumber = searchParams.PageNumber,
                    PageSize = totalPages,
                    Total = totalCount,
                    TrackingId = impressionTracking.Id
                };
            }
        }

        public async Task<IEnumerable<CategoryFilters>> GetAvailableFilters(
                SearchParams searchParams,
                IndexSearcher searcher,
                int filteredCount,
                int totalCount,
                BooleanQuery booleanQuery,
                int platformId = 1,
                long? companyId = null,
                Dictionary<string, ProductCardCategory> categoryMetadata = null)
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

            // Use provided categoryMetadata or load if not provided
            if (categoryMetadata == null)
            {
                categoryMetadata = await databaseContext.ProductCardCategories
                    .Where(p => p.CompanyId == companyId && p.PlatformId == platformId)
                    .AsNoTracking()
                    .ToDictionaryAsync(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
            }

            // Build lookup for parent-child relationships using dictionaries for O(1) lookups
            var childToParentLookup = categoryMetadata.Values
                .Where(p => !string.IsNullOrEmpty(p.ParentName))
                .ToDictionary(p => p.Name, p => p.ParentName!, StringComparer.OrdinalIgnoreCase);

            var parentToChildLookup = categoryMetadata.Values
                .Where(p => !string.IsNullOrEmpty(p.ParentName))
                .GroupBy(p => p.ParentName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

            // Group active filters by category for independent faceting
            // This allows multi-select within the same filter category
            var filtersByCategory = activeFilters
                .GroupBy(f => f.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Sample documents for facet aggregation (limit to avoid memory issues)
            const int maxDocsForFacets = 10000;
            const int batchSize = 1000;

            // Facet data storage
            var facetData = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var subcategoryData = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>(StringComparer.OrdinalIgnoreCase);

            // Build filter options using INDEPENDENT FACETING
            // For each category, we use a query that excludes that category's filters
            // This allows selecting multiple values within the same filter category
            const int maxOptionsPerCategory = 50;

            foreach (var category in allCategories)
            {
                // Skip categories of type None or TextField using dictionary lookup
                if (categoryMetadata.TryGetValue(category, out var cardCat) &&
                    (cardCat.ProductCardCategoryType == ProductCardCategoryType.None ||
                     cardCat.ProductCardCategoryType == ProductCardCategoryType.TextField))
                {
                    continue;
                }

                // INDEPENDENT FACETING: Build a query that EXCLUDES this category's filters
                // This allows the user to see ALL options in this category (for multi-select)
                // while other categories are filtered by the selected values
                var categoryExcludedQuery = BuildQueryExcludingCategory(filtersByCategory, category);
                Query baseQuery = categoryExcludedQuery.Clauses.Any()
                    ? categoryExcludedQuery
                    : new MatchAllDocsQuery();

                // First, discover unique values in this category by sampling documents
                var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var subCategoryCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

                // IMPORTANT: Track currently selected options for this category
                // These must ALWAYS appear in the UI so users can see/unselect their filters
                var selectedOptionsForCategory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (filtersByCategory.TryGetValue(category, out var selectedFilters))
                {
                    foreach (var filter in selectedFilters)
                    {
                        if (!string.IsNullOrWhiteSpace(filter.Option))
                        {
                            uniqueValues.Add(filter.Option);
                            selectedOptionsForCategory.Add(filter.Option);
                        }
                    }
                }

                var docsProcessed = 0;
                ScoreDoc? lastDoc = null;

                while (docsProcessed < maxDocsForFacets)
                {
                    var toFetch = Math.Min(batchSize, maxDocsForFacets - docsProcessed);
                    TopDocs topDocs = lastDoc == null
                        ? searcher.Search(baseQuery, toFetch)
                        : searcher.SearchAfter(lastDoc, baseQuery, toFetch);

                    if (topDocs.ScoreDocs.Length == 0) break;

                    foreach (var hit in topDocs.ScoreDocs)
                    {
                        var doc = searcher.Doc(hit.Doc);
                        var values = doc.GetValues(category);

                        foreach (var val in values)
                        {
                            if (string.IsNullOrWhiteSpace(val)) continue;

                            uniqueValues.Add(val);

                            // Handle subcategories if this category has children
                            if (parentToChildLookup.TryGetValue(category, out var childFieldName))
                            {
                                var subValues = doc.GetValues(childFieldName);
                                foreach (var subVal in subValues)
                                {
                                    if (string.IsNullOrWhiteSpace(subVal)) continue;

                                    if (!subCategoryCounts.TryGetValue(val, out var subDict))
                                    {
                                        subDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                        subCategoryCounts[val] = subDict;
                                    }

                                    // Just track that this subcategory exists for now
                                    subDict.TryGetValue(subVal, out var subCount);
                                    subDict[subVal] = subCount + 1;
                                }
                            }
                        }
                    }

                    lastDoc = topDocs.ScoreDocs[^1];
                    docsProcessed += topDocs.ScoreDocs.Length;
                }

                // Now get ACCURATE counts for each unique value using TotalHits
                // This ensures the count matches what you'd get when selecting that filter
                var filterCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var val in uniqueValues)
                {
                    // Build a query that combines the base query with this specific filter value
                    var valueQuery = new BooleanQuery();
                    if (categoryExcludedQuery.Clauses.Any())
                    {
                        valueQuery.Add(categoryExcludedQuery, Occur.MUST);
                    }
                    valueQuery.Add(new TermQuery(new Term(category, val)), Occur.MUST);

                    // Get accurate count using TotalHits
                    var countResult = searcher.Search(valueQuery, 1);
                    var count = countResult.TotalHits;

                    // Include options that have matching products OR are currently selected
                    // Selected options must always appear so users can see/unselect their filters
                    if (count > 0 || selectedOptionsForCategory.Contains(val))
                    {
                        filterCounts[val] = count;
                    }
                }

                // Store in facetData for reference
                facetData[category] = filterCounts;
                if (subCategoryCounts.Any())
                {
                    subcategoryData[category] = subCategoryCounts;
                }

                if (filterCounts.Count == 0) continue;

                var totalOptionsCount = filterCounts.Count;

                // Build all options, then sort: checked items first, then by count descending, then alphabetically
                var allOptions = filterCounts.Select(kvp =>
                {
                    var isChecked = activeFilters.Any(f =>
                        f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                        && f.Option.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

                    var option = new CategoryFilterOption
                    {
                        Name = kvp.Key,
                        Count = kvp.Value,
                        IsChecked = isChecked,
                        Subcategories = new List<CategoryFilterOption>()
                    };

                    // Add subcategories if they exist (also limited)
                    if (subCategoryCounts.TryGetValue(kvp.Key, out var subDict))
                    {
                        option.Subcategories = subDict
                            .OrderByDescending(s => activeFilters.Any(f =>
                                f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                                && f.Subcategory.Equals(s.Key, StringComparison.OrdinalIgnoreCase)))
                            .ThenByDescending(s => s.Value)
                            .ThenBy(s => s.Key)
                            .Take(maxOptionsPerCategory)
                            .Select(s => new CategoryFilterOption
                            {
                                Name = s.Key,
                                Count = s.Value,
                                IsChecked = activeFilters.Any(f =>
                                    f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                                    && f.Subcategory.Equals(s.Key, StringComparison.OrdinalIgnoreCase))
                            })
                            .ToList();
                    }

                    return option;
                })
                // Sort: checked first, then by count (most popular), then alphabetically
                .OrderByDescending(o => o.IsChecked)
                .ThenByDescending(o => o.Count)
                .ThenBy(o => o.Name)
                .Take(maxOptionsPerCategory)
                .ToList();

                // Get category type and order from metadata
                ProductCardCategoryType? categoryType = null;
                Dictionary<string, string>? colorHexMapping = null;
                int categoryOrder = 0;
                if (categoryMetadata != null && categoryMetadata.TryGetValue(category, out var catMeta))
                {
                    categoryType = catMeta.ProductCardCategoryType;
                    categoryOrder = catMeta.Order;

                    // Parse color hex mapping for ColorField types
                    if (categoryType == ProductCardCategoryType.ColorField && !string.IsNullOrWhiteSpace(catMeta.ColorHexMappingJson))
                    {
                        try
                        {
                            colorHexMapping = JsonConvert.DeserializeObject<Dictionary<string, string>>(catMeta.ColorHexMappingJson);
                        }
                        catch
                        {
                            // Invalid JSON, ignore
                        }
                    }
                }

                categoryOptions.Add(new CategoryFilters
                {
                    Category = category,
                    Options = allOptions,
                    TotalOptionsCount = totalOptionsCount,
                    HasMoreOptions = totalOptionsCount > maxOptionsPerCategory,
                    CategoryType = categoryType,
                    ColorHexMapping = colorHexMapping,
                    Order = categoryOrder
                });
            }

            // Sort by Order first (0 values go last), then alphabetically
            return categoryOptions
                .OrderBy(c => c.Order == 0 ? int.MaxValue : c.Order)
                .ThenBy(c => c.Category);
        }

        /// <summary>
        /// Get paginated/searchable filter options for a specific category (e.g., load more brands)
        /// Supports independent faceting - filters from other categories are applied, but not from the requested category
        /// </summary>
        public async Task<FilterOptionsResponse> GetFilterOptions(FilterOptionsRequest request)
        {
            var containerLocation = appSettings.LuceneSearch.Container;
            containerLocation += "/" + (request.OemCompanyId?.ToString() ?? "0");
            containerLocation += "/" + request.PlatformId;

            var versionInformation = await GetVersionFile(containerLocation);
            containerLocation += "/" + versionInformation.ToString();

            string cachePath = Path.Combine(
                Environment.GetEnvironmentVariable("HOME") ?? string.Empty,
                "cache", "LuceneCache", (request.OemCompanyId?.ToString() ?? "0"), request.PlatformId.ToString(), versionInformation.ToString()
            );

            if (!System.IO.Directory.Exists(cachePath))
                System.IO.Directory.CreateDirectory(cachePath);

            using (var cacheDirectory = new SimpleFSDirectory(new DirectoryInfo(cachePath)))
            using (var azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, containerLocation, cacheDirectory))
            using (var reader = DirectoryReader.Open(azureDirectory))
            {
                var searcher = new IndexSearcher(reader);

                // INDEPENDENT FACETING: Build query excluding the requested category's filters
                // This allows showing ALL options for this category while respecting other category filters
                var activeFilters = request.ActiveFilters?.ToList() ?? new List<SearchParamFilter>();
                var filtersByCategory = activeFilters
                    .GroupBy(f => f.Category, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                var categoryExcludedQuery = BuildQueryExcludingCategory(filtersByCategory, request.FilterCategory);
                Query baseQuery = categoryExcludedQuery.Clauses.Any()
                    ? categoryExcludedQuery
                    : new MatchAllDocsQuery();

                // First, discover unique values in this category by sampling documents
                var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // IMPORTANT: Track currently selected options for this category
                // These must ALWAYS appear in the UI so users can see/unselect their filters
                var selectedOptionsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (request.SelectedOptions != null)
                {
                    foreach (var selectedOption in request.SelectedOptions)
                    {
                        if (!string.IsNullOrWhiteSpace(selectedOption))
                        {
                            uniqueValues.Add(selectedOption);
                            selectedOptionsSet.Add(selectedOption);
                        }
                    }
                }

                // Sample documents for facet aggregation (limit to avoid memory issues)
                const int maxDocsForFacets = 50000;
                const int batchSize = 1000;
                var docsProcessed = 0;
                ScoreDoc? lastDoc = null;

                while (docsProcessed < maxDocsForFacets)
                {
                    var remaining = maxDocsForFacets - docsProcessed;
                    var toFetch = Math.Min(batchSize, remaining);

                    TopDocs topDocs = lastDoc == null
                        ? searcher.Search(baseQuery, toFetch)
                        : searcher.SearchAfter(lastDoc, baseQuery, toFetch);

                    if (topDocs.ScoreDocs.Length == 0) break;

                    foreach (var hit in topDocs.ScoreDocs)
                    {
                        var doc = searcher.Doc(hit.Doc);
                        var values = doc.GetValues(request.FilterCategory);

                        foreach (var val in values)
                        {
                            if (string.IsNullOrWhiteSpace(val)) continue;
                            uniqueValues.Add(val);
                        }
                    }

                    lastDoc = topDocs.ScoreDocs[^1];
                    docsProcessed += topDocs.ScoreDocs.Length;
                }

                // Now get ACCURATE counts for each unique value using TotalHits
                // This ensures the count matches what you'd get when selecting that filter
                var filterCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var val in uniqueValues)
                {
                    // Build a query that combines the base query with this specific filter value
                    var valueQuery = new BooleanQuery();
                    if (categoryExcludedQuery.Clauses.Any())
                    {
                        valueQuery.Add(categoryExcludedQuery, Occur.MUST);
                    }
                    valueQuery.Add(new TermQuery(new Term(request.FilterCategory, val)), Occur.MUST);

                    // Get accurate count using TotalHits
                    var countResult = searcher.Search(valueQuery, 1);
                    var count = countResult.TotalHits;

                    // Include options that have matching products OR are currently selected
                    // Selected options must always appear so users can see/unselect their filters
                    if (count > 0 || selectedOptionsSet.Contains(val))
                    {
                        filterCounts[val] = count;
                    }
                }

                // Filter by search term if provided
                var filteredOptions = filterCounts.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var searchTermLower = request.SearchTerm.ToLowerInvariant();
                    filteredOptions = filteredOptions.Where(kvp =>
                        kvp.Key.ToLowerInvariant().Contains(searchTermLower));
                }

                var totalCount = filteredOptions.Count();

                // Build options with selected items prioritized
                var selectedSet = request.SelectedOptions?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var allOptions = filteredOptions
                    .Select(kvp => new CategoryFilterOption
                    {
                        Name = kvp.Key,
                        Count = kvp.Value,
                        IsChecked = selectedSet.Contains(kvp.Key)
                    })
                    // Sort: checked first, then by count (most popular), then alphabetically
                    .OrderByDescending(o => o.IsChecked)
                    .ThenByDescending(o => o.Count)
                    .ThenBy(o => o.Name)
                    .ToList();

                // Apply pagination
                var skip = (request.PageNumber - 1) * request.PageSize;
                var pagedOptions = allOptions.Skip(skip).Take(request.PageSize).ToList();

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                return new FilterOptionsResponse
                {
                    Category = request.FilterCategory,
                    Options = pagedOptions,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    HasMorePages = request.PageNumber < totalPages
                };
            }
        }

        public async Task Clicked(Guid trackingId, string productOrServiceId)
        {
            var analytics = await databaseContext.AnalyticsMarketplaceImpressionsClicks
                .Where(s => s.Id == trackingId)
                .FirstOrDefaultAsync();

            if (analytics != null)
            {
                analytics.ProductOrServiceClicked = productOrServiceId;
                await databaseContext.SaveChangesAsync();
            }
        }

        public async Task<MarketplaceAnalytics> GetAnalytics(int platformId, long? oemCompanyId = null, int days = 30)
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-days);

            var analyticsQuery = databaseContext.AnalyticsMarketplaceImpressionsClicks
                .AsNoTracking()
                .Where(a => a.Platform == platformId && a.Created >= cutoffDate);

            if (oemCompanyId.HasValue)
            {
                analyticsQuery = analyticsQuery.Where(a => a.OemCompanyId == oemCompanyId);
            }

            var analyticsData = await analyticsQuery.ToListAsync();

            // Calculate CTR
            var totalImpressions = analyticsData.Count;
            var totalClicks = analyticsData.Count(a => !string.IsNullOrEmpty(a.ProductOrServiceClicked));
            var clickThroughRate = totalImpressions > 0 ? (double)totalClicks / totalImpressions * 100 : 0;

            // Most clicked products
            var popularProducts = analyticsData
                .Where(a => !string.IsNullOrEmpty(a.ProductOrServiceClicked))
                .GroupBy(a => a.ProductOrServiceClicked)
                .Select(g => new PopularProduct
                {
                    ProductId = g.Key!,
                    Clicks = g.Count()
                })
                .OrderByDescending(p => p.Clicks)
                .Take(20)
                .ToList();

            // Popular filters
            var popularFilters = analyticsData
                .Where(a => !string.IsNullOrEmpty(a.JSONFilterSelected) && a.JSONFilterSelected != "[]")
                .SelectMany(a =>
                {
                    try
                    {
                        var filters = JsonConvert.DeserializeObject<List<SearchParamFilter>>(a.JSONFilterSelected);
                        return filters ?? new List<SearchParamFilter>();
                    }
                    catch
                    {
                        return new List<SearchParamFilter>();
                    }
                })
                .GroupBy(f => new { f.Category, f.Option })
                .Select(g => new PopularFilter
                {
                    Category = g.Key.Category,
                    Option = g.Key.Option,
                    Usage = g.Count()
                })
                .OrderByDescending(f => f.Usage)
                .Take(20)
                .ToList();

            // Daily trends
            var dailyTrends = analyticsData
                .GroupBy(a => a.Created.Date)
                .Select(g => new DailyTrend
                {
                    Date = g.Key,
                    Impressions = g.Count(),
                    Clicks = g.Count(a => !string.IsNullOrEmpty(a.ProductOrServiceClicked))
                })
                .OrderBy(d => d.Date)
                .ToList();

            return new MarketplaceAnalytics
            {
                PlatformId = platformId,
                OemCompanyId = oemCompanyId,
                PeriodDays = days,
                TotalImpressions = totalImpressions,
                TotalClicks = totalClicks,
                ClickThroughRate = Math.Round(clickThroughRate, 2),
                PopularProducts = popularProducts,
                PopularFilters = popularFilters,
                DailyTrends = dailyTrends
            };
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
                    Order = category?.Order ?? 0,
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

        /// <summary>
        /// Builds a Lucene query that includes filters from all categories EXCEPT the specified one.
        /// This enables independent faceting - allowing multi-select within a single filter category.
        /// </summary>
        private BooleanQuery BuildQueryExcludingCategory(
            Dictionary<string, List<SearchParamFilter>> filtersByCategory,
            string excludeCategory)
        {
            var query = new BooleanQuery();

            foreach (var kvp in filtersByCategory)
            {
                // Skip the category we want to exclude (for independent faceting)
                if (kvp.Key.Equals(excludeCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Build OR query for this category's filters (within category = OR)
                var categoryQuery = new BooleanQuery { MinimumNumberShouldMatch = 1 };
                foreach (var filter in kvp.Value)
                {
                    if (!string.IsNullOrWhiteSpace(filter.Subcategory))
                    {
                        // Handle parent-child filter pairs
                        var pairQ = new BooleanQuery
                        {
                            { new TermQuery(new Term("Category", filter.Option)), Occur.MUST },
                            { new TermQuery(new Term("Categories", filter.Subcategory)), Occur.MUST }
                        };
                        categoryQuery.Add(pairQ, Occur.SHOULD);
                    }
                    else
                    {
                        // Simple term filter
                        categoryQuery.Add(new TermQuery(new Term(filter.Category, filter.Option)), Occur.SHOULD);
                    }
                }

                // Across categories = AND
                query.Add(categoryQuery, Occur.MUST);
            }

            return query;
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