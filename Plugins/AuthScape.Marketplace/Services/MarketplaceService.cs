﻿using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.Attributes;
using AuthScape.PrivateLabel.Models;
using CoreBackpack.Time;
using CsvHelper;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Services.Context;
using Services.Database;
using System;
using System.Globalization;
using System.Reflection;
using static AuthScape.Marketplace.Services.MarketplaceService;

namespace AuthScape.Marketplace.Services
{
    public interface IMarketplaceService
    {
        Task<SearchResult2> SearchProducts(SearchParams searchParams);
        Task Clicked(int platformId, string productOrServiceId, long? CompanyId = null);
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
            var containerLocation = appSettings.LuceneSearch.Container;
            if (searchParams.OemCompanyId != null)
            {
                containerLocation += "/" + searchParams.OemCompanyId;
            }
            else
            {
                containerLocation += "/" + "0";
            }

            containerLocation += "/" + searchParams.PlatformId;

            AzureDirectory azureDirectory = new AzureDirectory(appSettings.LuceneSearch.StorageConnectionString, containerLocation);
            using var reader = DirectoryReader.Open(azureDirectory);
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

            ScoreDoc[] showAllPossibleHits = searcher.Search(new MatchAllDocsQuery(), int.MaxValue).ScoreDocs;

            ScoreDoc[] filteredOutHits;
            if (!hasFilters)
            {
                filteredOutHits = showAllPossibleHits;
            }
            else
            {
                filteredOutHits = searcher.Search(booleanQuery, int.MaxValue).ScoreDocs;
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
                    record.Add(new ProductResult { Name = field.Name, Value = doc.Get(field.Name) });

                    if (field.Name == "ReferenceId")
                    {
                        listOfReferenceIds.Add(doc.Get(field.Name));
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


        async Task<HashSet<CategoryFilters>> GetAvailableFilters(
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
                    if (field.Name == "Id" || field.Name == "Name" || field.Name == "ReferenceId") continue;
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
                                var subCat = doc2.Get("Category");
                                // And "ParentCategory" holds the name of the parent option for the subcategory
                                var parentCat = doc2.Get("ParentCategory");

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
                            }) : null
                        })
                    };

                    categoryOptions.Add(categoryFilter);
                }
            }

            return categoryOptions;
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
    }
}
