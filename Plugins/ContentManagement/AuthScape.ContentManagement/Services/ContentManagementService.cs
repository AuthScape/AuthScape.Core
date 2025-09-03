using AuthScape.ContentManagement.Models;
using AuthScape.Models;
using AuthScape.Services;
using AuthScape.Services.Azure.Storage;
using CoreBackpack;
using CoreBackpack.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;

namespace AuthScape.ContentManagement.Services
{
    public interface IContentManagementService
    {
        Task<PagedList<Page>> GetPages(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12, long? privateLabelCompanyId = null);
        Task<PagedList<PageImageAsset>> GetPageAssets(string search, int sort, int offset = 1, int length = 12, long? privateLabelCompanyId = null);
        Task<PagedList<PageBlockList>> GetPageBlockList(string search, int sort, int offset = 1, int length = 12, long? privateLabelCompanyId = null);
        Task<List<PageType>> GetPageTypes();
        Task<List<PageRoot>> GetPageRoots(long? privateLabelCompanyId = null);
        Task<Page> GetPage(Guid pageId);
        Task<Guid> CreateNewPage(string title, long pageTypeId, long? pageRootId, string description, int? recursion, string slug, long? PrivateLabelCompanyId = null);
        Task UpdatePageContent(Guid pageId, string data);
        Task UpdatePage(Guid? pageId, string title, long pageTypeId, long? pageRootId, string description, int? recursion, string slug, long? PrivateLabelCompanyId = null);
        Task<Guid> CreateNewAsset(string title, IFormFile file, string description, long? PrivateLabelCompanyId = null);
        Task UpdateAsset(Guid? assetId, string title, string description, long? PrivateLabelCompanyId = null);
        Task<Guid> CreateNewBlockList(string title, string? email, string? keyword, string description, long? PrivateLabelCompanyId = null);
        Task UpdateBlockList(Guid? assetId, string title, string? email, string? keyword, string description, long? PrivateLabelCompanyId = null);
        Task RemovePage(Guid pageId);
        Task RemoveAsset(Guid assetId);
        Task RemoveBlockList(Guid blockId);
        Task<AuthScape.ContentManagement.Models.Page?> GetPageWithSlug(List<string>? slugs, string? Host = null);
        Task<Guid> CreatePageDuplication(Guid pageId, long oemCompanyId);
        Task<List<PageImageAsset>> GetPageImageAssets(long? oemCompanyId);
        Task<Page?> GetHomepage();
    }
    public class ContentManagementService : IContentManagementService
    {
        readonly DatabaseContext databaseContext;
        readonly IUserManagementService userService;
        readonly ISlugService slugService;
        readonly IAzureBlobStorage azureBlobStorage;
        readonly AppSettings appSettings;
        public ContentManagementService(DatabaseContext databaseContext, IUserManagementService userService, ISlugService slugService, IAzureBlobStorage azureBlobStorage, IOptions<AppSettings> appSettings)
        {
            this.databaseContext = databaseContext;
            this.userService = userService;
            this.slugService = slugService;
            this.azureBlobStorage = azureBlobStorage;
            this.appSettings = appSettings.Value;
        }

        protected string GetStorageType(string container)
        {
            switch (appSettings.Stage)
            {
                case Stage.Development:
                    container += "-dev";
                    break;
                case Stage.Staging:
                    container += "-staging";
                    break;
            }
            return container;
        }

        public async Task<List<PageImageAsset>> GetPageImageAssets(long? oemCompanyId)
        {
            return await databaseContext.PageImageAssets
                .AsNoTracking()
                .Where(pia => pia.CompanyId == oemCompanyId)
                .ToListAsync();
        }

        
        public async Task<Page?> GetHomepage()
        {
            var homepagePageType = await databaseContext.PageTypes.Where(pt => pt.IsHomepage).FirstOrDefaultAsync();

            if (homepagePageType == null) { return null; }

            var homepage = await databaseContext.Pages.Where(p => p.PageTypeId == homepagePageType.Id).Select(p => new Page
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Content = p.Content,
                CompanyId = p.CompanyId,
                Description = p.Description,
                Created = p.Created,
                LastUpdated = p.LastUpdated,
                PageTypeId = p.PageTypeId,
                Recursion = p.Recursion,
                TypeTitle = p.PageType.Title,
            }).FirstOrDefaultAsync();

            //if (homepage == null)
            //{
            //    throw new Exception("Homepage does not exist");
            //}

            return homepage;
        }

        public async Task UpdatePage(Guid? pageId, string title, long pageTypeId, long? pageRootId, string description, int? recursion, string slug, long? PrivateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (pageId == null) { throw new Exception("Id must be provided"); }


            var homepagePageType = await databaseContext.PageTypes.Where(pt => pt.IsHomepage).FirstOrDefaultAsync();

            if (homepagePageType != null)
            {
                if (pageTypeId == homepagePageType.Id)
                {
                    var homepageExisted = await databaseContext.Pages.Where(p => p.PageTypeId == homepagePageType.Id && p.Id != pageId && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();

                    if (homepageExisted != null)
                    {
                        throw new Exception("Homepage already existed");
                    }
                }
            }

            var slugExisted = await databaseContext.Pages.Where(p => p.Slug == slug && p.Id != pageId && p.PageRootId == pageRootId && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();
            if (slugExisted != null) { throw new Exception("Same Slug already existed"); }

            var page = await databaseContext.Pages.Where(p => p.Id == pageId && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();

            if (page == null) { throw new Exception("Page does not exist"); }

            page.Title = title;
            page.PageTypeId = pageTypeId;
            page.Description = description;
            page.LastUpdated = DateTimeOffset.Now;
            page.Slug = slug;
            page.PageTypeId = pageTypeId;
            page.PageRootId = pageRootId;
            page.Recursion = recursion;
            page.Slug = slug;

            await databaseContext.SaveChangesAsync();
        }
        public async Task<Guid> CreateNewPage(string title, long pageTypeId, long? pageRootId, string description, int? recursion, string slug, long? PrivateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var homepagePageType = await databaseContext.PageTypes.Where(pt => pt.IsHomepage).FirstOrDefaultAsync();

            if (homepagePageType != null)
            {
                if (pageTypeId == homepagePageType.Id)
                {
                    var homepageExisted = await databaseContext.Pages.Where(p => p.PageTypeId == homepagePageType.Id && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();

                    if (homepageExisted != null)
                    {
                        throw new Exception("Homepage already existed");
                    }
                }
            }

            var slugExisted = await databaseContext.Pages.Where(p => p.Slug == slug && p.PageRootId == pageRootId && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();
            if (slugExisted != null) { throw new Exception("Same Slug already existed"); }

            var page = new Page
            {
                Title = title,
                CompanyId = PrivateLabelCompanyId,
                Description = description,
                Slug = slug,
                Created = DateTimeOffset.Now,
                LastUpdated = DateTimeOffset.Now,
                PageTypeId = pageTypeId,
                PageRootId = pageRootId,
                Recursion = recursion,
            };

            databaseContext.Pages.Add(page);
            await databaseContext.SaveChangesAsync();
            return page.Id;
        }

        public async Task<Guid> CreatePageDuplication(Guid pageId, long oemCompanyId)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var page = await databaseContext.Pages.AsNoTracking().Where(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page == null)
            {
                throw new Exception("Page does not exist");
            }

            var homepagePageType = await databaseContext.PageTypes.Where(pt => pt.IsHomepage).FirstOrDefaultAsync();

            if (homepagePageType != null)
            {
                if (page.PageTypeId == homepagePageType.Id)
                {

                    throw new Exception("Homepage already existed");
                }
            }

            var copypage = new Page
            {
                Title = "",
                CompanyId = oemCompanyId,
                Description = page.Description,
                Content = page.Content,
                Slug = "",
                Created = DateTimeOffset.Now,
                LastUpdated = DateTimeOffset.Now,
                PageTypeId = page.PageTypeId,
                PageRootId = page.PageRootId,
                Recursion = page.Recursion,
            };

            databaseContext.Pages.Add(copypage);
            await databaseContext.SaveChangesAsync();
            copypage.Title = page.Title + "-" + copypage.Id;
            copypage.Slug = page.Slug + "-" + copypage.Id;
            await databaseContext.SaveChangesAsync();
            return copypage.Id;
        }

        public async Task<Guid> CreateNewAsset(string title, IFormFile file, string description, long? PrivateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var containerName = GetStorageType("frontendassets");


            var asset = new PageImageAsset
            {
                Title = title,
                FileName = file.FileName,
                Url = "",
                CompanyId = PrivateLabelCompanyId,
                Description = description,
                Created = DateTimeOffset.Now,
                LastUpdated = DateTimeOffset.Now,
            };

            databaseContext.PageImageAssets.Add(asset);
            await databaseContext.SaveChangesAsync();

            var filesName = await azureBlobStorage.UploadFile(file, containerName, asset.Id.ToString());

            var url = "https://axiomna.blob.core.windows.net/" + containerName + "/" + asset.Id.ToString() + Path.GetExtension(file.FileName);

            asset.Url = url;

            await databaseContext.SaveChangesAsync();

            return asset.Id;
        }


        public async Task UpdateAsset(Guid? assetId, string title, string description, long? PrivateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var asset = await databaseContext.PageImageAssets.Where(p => p.Id == assetId && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();
            if (asset == null) { throw new Exception("Page does not exist"); }

            asset.Title = title;
            asset.Description = description;
            asset.LastUpdated = DateTimeOffset.Now;

            await databaseContext.SaveChangesAsync();
        }


        public async Task<Guid> CreateNewBlockList(string title, string? email, string? keyword, string description, long? PrivateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var blocklist = new PageBlockList
            {
                Title = title,
                Email = email,
                Keyword = keyword,
                CompanyId = PrivateLabelCompanyId,
                Description = description,
                Created = DateTimeOffset.Now,
                LastUpdated = DateTimeOffset.Now,
            };

            databaseContext.PageBlockLists.Add(blocklist);
            await databaseContext.SaveChangesAsync();

            return blocklist.Id;
        }


        public async Task UpdateBlockList(Guid? blocklistId, string title, string? email, string? keyword, string description, long? PrivateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var blockList = await databaseContext.PageBlockLists.Where(p => p.Id == blocklistId && p.CompanyId == PrivateLabelCompanyId).FirstOrDefaultAsync();
            if (blockList == null) { throw new Exception("BlockList does not exist"); }

            blockList.Title = title;
            blockList.Email = email;
            blockList.Keyword = keyword;
            blockList.Description = description;
            blockList.LastUpdated = DateTimeOffset.Now;

            await databaseContext.SaveChangesAsync();
        }

        public async Task<Page> GetPage(Guid pageId)
        {
            var page = await databaseContext.Pages
                .AsNoTracking()
                .Include(p => p.PageType).Include(p => p.PageRoot)
                .Where(pq => pq.Id == pageId)
                .Select(p => new Page
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Content = p.Content,
                    CompanyId = p.CompanyId,
                    Description = p.Description,
                    Created = p.Created,
                    LastUpdated = p.LastUpdated,
                    PageTypeId = p.PageTypeId,
                    PageRootId = p.PageRootId,
                    Recursion = p.Recursion,
                    TypeTitle = p.PageType.Title,
                    RootUrl = p.PageRoot.RootUrl,
                }).FirstOrDefaultAsync();

            if (page == null)
            {
                throw new Exception("Page does not exist");
            }

            return page;
        }
        public async Task<PagedList<Page>> GetPages(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12, long? privateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();

            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
            }

            var pageQuery = databaseContext.Pages
                .AsNoTracking()
                .Include(pt => pt.PageType)
                .Include(pt => pt.PageRoot)
                .Where(pq =>
                    pq.CompanyId == privateLabelCompanyId &&
                    (string.IsNullOrWhiteSpace(search) || pq.Title.ToLower().Contains(search)));

            if (chipFilters != null && chipFilters.Length > 0)
            {
                pageQuery = pageQuery.Where(pq => chipFilters.Contains(pq.PageTypeId));
            }

            switch (sort)
            {
                case 1:
                    pageQuery = pageQuery.OrderBy(pq => pq.Title);
                    break;
                case 2:
                    pageQuery = pageQuery.OrderByDescending(pq => pq.Title);
                    break;
                case 3:
                    pageQuery = pageQuery.OrderBy(pq => pq.LastUpdated);
                    break;
                case 4:
                    pageQuery = pageQuery.OrderByDescending(pq => pq.LastUpdated);
                    break;
            }

            var pages = await pageQuery
                .Select(p => new Page
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Content = p.Content,
                    CompanyId = p.CompanyId,
                    Description = p.Description,
                    Created = p.Created,
                    LastUpdated = p.LastUpdated,
                    PageTypeId = p.PageTypeId,
                    PageRootId = p.PageRootId,
                    Recursion = p.Recursion,
                    TypeTitle = p.PageType.Title,
                    RootUrl = p.PageRoot.RootUrl,
                })
                .ToPagedResultAsync(offset - 1, length);

            return pages;
        }

        public async Task<List<PageType>> GetPageTypes()
        {
            var pageTypes = await databaseContext.PageTypes.AsNoTracking().ToListAsync();
            return pageTypes;
        }

        public async Task<List<PageRoot>> GetPageRoots(long? privateLabelCompanyId = null)
        {
            var pageRoots = await databaseContext.PageRoots.AsNoTracking().Where(z => z.CompanyId == privateLabelCompanyId).ToListAsync();
            return pageRoots;
        }
        public async Task RemovePage(Guid pageId)
        {
            var page = await databaseContext.Pages.Where(pt => pt.Id == pageId).FirstOrDefaultAsync();
            if (page == null) { throw new Exception("Page does not exist"); }
            databaseContext.Pages.Remove(page);
            await databaseContext.SaveChangesAsync();
        }

        public async Task RemoveAsset(Guid assetId)
        {
            var asset = await databaseContext.PageImageAssets.Where(pa => pa.Id == assetId).FirstOrDefaultAsync();
            if (asset == null) { throw new Exception("Asset does not exist"); }
            databaseContext.PageImageAssets.Remove(asset);
            await databaseContext.SaveChangesAsync();
        }

        public async Task RemoveBlockList(Guid blockId)
        {
            var blockList = await databaseContext.PageBlockLists.Where(pa => pa.Id == blockId).FirstOrDefaultAsync();
            if (blockList == null) { throw new Exception("BlockList does not exist"); }
            databaseContext.PageBlockLists.Remove(blockList);
            await databaseContext.SaveChangesAsync();
        }
        public async Task UpdatePageContent(Guid pageId, string data)
        {
            var page = await databaseContext.Pages.Where(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page == null) { throw new Exception("Page does not exist"); }
            page.Content = data;
            page.LastUpdated = DateTime.Now;
            await databaseContext.SaveChangesAsync();
        }


        public async Task<AuthScape.ContentManagement.Models.Page?> GetPageWithSlug(List<string>? slugs, string? Host = null)
        {
            if (Host.Contains("localhost"))
            {
                Host = "http://" + Host;
            }
            else
            {
                Host = "https://" + Host;
            }

            //var slug = String.Join("/", slugs);
            var privateLabelCompanyId = await databaseContext.DnsRecords
                .AsNoTracking()
                .Where(d => d.Domain.ToLower() == Host.ToLower())
                .Select(z => z.CompanyId)
                .FirstOrDefaultAsync();

            var page = databaseContext.Pages
                .AsNoTracking()
                .Include(p => p.PageType)
                .Where(z => z.Recursion == null && z.CompanyId == privateLabelCompanyId);

            if (slugs != null)
            {
                if (slugs.Count() == 1)
                {
                    // this is only on the slug
                    var pageSlug = slugs.FirstOrDefault();

                    page = page.Where(z => z.Slug == pageSlug);
                }
                else
                {
                    // this has a root
                    var pageSlug = slugs.LastOrDefault();

                    var rootPageSlug = slugs.FirstOrDefault();

                    var rootPage = await databaseContext.PageRoots
                        .AsNoTracking()
                        .Where(z => z.CompanyId == privateLabelCompanyId && z.RootUrl == rootPageSlug)
                        .FirstOrDefaultAsync();

                    if (rootPage != null)
                    {
                        page = page.Where(z => z.PageRootId == rootPage.Id && z.Slug == pageSlug);
                    }
                }
            }
            else
            {
                page = page.Where(z => z.PageType.IsHomepage);
            }

            var _page = await page.Select(p => new AuthScape.ContentManagement.Models.Page
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Content = p.Content,
                CompanyId = p.CompanyId,
                Description = p.Description,
                Created = p.Created,
                LastUpdated = p.LastUpdated,
                PageTypeId = p.PageTypeId,
                Recursion = p.Recursion,
                TypeTitle = p.PageType.Title,

            }).FirstOrDefaultAsync();

            //if (_page == null)
            //{
            //    throw new BadRequestException("Page does not exist");
            //}

            return _page;
        }

        public async Task<PagedList<PageImageAsset>> GetPageAssets(string search, int sort, int offset = 1, int length = 12, long? privateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();

            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
            }

            var pageAssetQuery = databaseContext.PageImageAssets
                .AsNoTracking()
                .Where(pq =>
                    pq.CompanyId == privateLabelCompanyId &&
                    (string.IsNullOrWhiteSpace(search) || pq.Title.ToLower().Contains(search)));


            switch (sort)
            {
                case 1:
                    pageAssetQuery = pageAssetQuery.OrderBy(pq => pq.Title);
                    break;
                case 2:
                    pageAssetQuery = pageAssetQuery.OrderByDescending(pq => pq.Title);
                    break;
                case 3:
                    pageAssetQuery = pageAssetQuery.OrderBy(pq => pq.LastUpdated);
                    break;
                case 4:
                    pageAssetQuery = pageAssetQuery.OrderByDescending(pq => pq.LastUpdated);
                    break;
            }

            var pageAssets = await pageAssetQuery
                .Select(p => new PageImageAsset
                {
                    Id = p.Id,
                    Title = p.Title,
                    FileName = p.FileName,
                    Description = p.Description,
                    Url = p.Url,
                    CompanyId = p.CompanyId,
                    Created = p.Created,
                    LastUpdated = p.LastUpdated,
                })
                .ToPagedResultAsync(offset - 1, length);

            return pageAssets;
        }


        public async Task<PagedList<PageBlockList>> GetPageBlockList(string search, int sort, int offset = 1, int length = 12, long? privateLabelCompanyId = null)
        {
            var signedInUser = await userService.GetSignedInUser();

            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
            }


            var pageBlockListQuery = databaseContext.PageBlockLists
                .AsNoTracking()
                .Where(pq =>
                    pq.CompanyId == privateLabelCompanyId &&
                    (string.IsNullOrWhiteSpace(search) || pq.Title.ToLower().Contains(search)));


            switch (sort)
            {
                case 1:
                    pageBlockListQuery = pageBlockListQuery.OrderBy(pq => pq.Title);
                    break;
                case 2:
                    pageBlockListQuery = pageBlockListQuery.OrderByDescending(pq => pq.Title);
                    break;
                case 3:
                    pageBlockListQuery = pageBlockListQuery.OrderBy(pq => pq.LastUpdated);
                    break;
                case 4:
                    pageBlockListQuery = pageBlockListQuery.OrderByDescending(pq => pq.LastUpdated);
                    break;
            }

            var pageAssets = await pageBlockListQuery
                .Select(p => new PageBlockList
                {
                    Id = p.Id,
                    Title = p.Title,
                    Email = p.Email,
                    Keyword = p.Keyword,
                    Description = p.Description,
                    CompanyId = p.CompanyId,
                    Created = p.Created,
                    LastUpdated = p.LastUpdated,
                })
                .ToPagedResultAsync(offset - 1, length);

            return pageAssets;
        }
    }
}
