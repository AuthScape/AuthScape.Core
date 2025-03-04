using AuthScape.ContentManagement.Models;
using AuthScape.DocumentReader.Controllers;
using AuthScape.Models;
using AuthScape.Services;
using AuthScape.Services.Azure.Storage;
using CoreBackpack;
using CoreBackpack.Services;
using CoreBackpack.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using System.Text;

namespace AuthScape.ContentManagement.Services
{
    public interface IContentManagementService
    {
        Task<PagedList<Page>> GetPages(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12);
        Task<PagedList<PageImageAsset>> GetPageAssets(string search, int sort, int offset = 1, int length = 12);
        Task<List<PageType>> GetPageTypes();
        Task<List<PageRoot>> GetPageRoots();
        Task<Page> GetPage(Guid pageId);
        Task<Guid> CreateNewPage(string title, long pageTypeId, string description, int? recursion, string slug);
        Task UpdatePageContent(Guid pageId, string data);
        Task UpdatePage(Guid? pageId, string title, long pageTypeId, string description, int? recursion, string slug);
        Task<Guid> CreateNewAsset(string title, IFormFile file, string description);
        Task UpdateAsset(Guid? assetId, string title, string description);
        Task RemovePage(Guid pageId);
        Task RemoveAsset(Guid assetId);
        Task<Page> GetPageWithSlug(string slug);
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

        public async Task UpdatePage(Guid? pageId, string title, long pageTypeId, string description, int? recursion, string slug)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (pageId == null) { throw new Exception("Id must be provided"); }


            var homepagePageType = await databaseContext.PageTypes.Where(pt => pt.IsHomepage).FirstOrDefaultAsync();

            if (homepagePageType != null)
            {
                if (pageTypeId == homepagePageType.Id)
                {
                    var homepageExisted = await databaseContext.Pages.Where(p => p.PageTypeId == homepagePageType.Id && p.Id != pageId).FirstOrDefaultAsync();

                    if (homepageExisted != null)
                    {
                        throw new Exception("Homepage already existed");
                    }
                }
            }

            var slugExisted = await databaseContext.Pages.Where(p => p.Slug == slug && p.Id != pageId).FirstOrDefaultAsync();
            if (slugExisted != null) { throw new Exception("Same Slug already existed"); }
            
            var page = await databaseContext.Pages.Where(p => p.Id == pageId).FirstOrDefaultAsync();
          
            if (page == null) { throw new Exception("Page does not exist"); }

            page.Title = title;
            page.PageTypeId = pageTypeId;
            page.Description = description;
            page.LastUpdated = DateTimeOffset.Now;
            page.Slug = slug;
            page.PageTypeId = pageTypeId;
            page.Recursion = recursion;
            page.Slug = slug;

            await databaseContext.SaveChangesAsync();
        }
        public async Task<Guid> CreateNewPage(string title, long pageTypeId, string description, int? recursion, string slug)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var homepagePageType = await databaseContext.PageTypes.Where(pt => pt.IsHomepage).FirstOrDefaultAsync();

            if (homepagePageType != null)
            {
                if (pageTypeId == homepagePageType.Id)
                {
                    var homepageExisted = await databaseContext.Pages.Where(p => p.PageTypeId == homepagePageType.Id).FirstOrDefaultAsync();

                    if (homepageExisted != null)
                    {
                        throw new Exception("Homepage already existed");
                    }
                }
            }

            var slugExisted = await databaseContext.Pages.Where(p => p.Slug == slug).FirstOrDefaultAsync();
            if (slugExisted != null) { throw new Exception("Same Slug already existed"); }

            var page = new Page          
            {   Title = title,
                CompanyId = signedInUser.CompanyId, 
                Description = description,
                Slug = slug, 
                Created = DateTimeOffset.Now, 
                LastUpdated = DateTimeOffset.Now,
                PageTypeId = pageTypeId,
                Recursion = recursion,
            };

            databaseContext.Pages.Add(page);
            await databaseContext.SaveChangesAsync();
            return page.Id;
        }

        public async Task<Guid> CreateNewAsset(string title, IFormFile file, string description)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var containerName = GetStorageType("frontendassets");

            var filesName = await azureBlobStorage.UploadFile(file, containerName, file.Name);

            var url = "https://axiomna.blob.core.windows.net/" + containerName + "/" + filesName;

            var asset = new PageImageAsset
            {
                Title = title,
                FileName = file.FileName,
                Url = url,
                CompanyId = (long)signedInUser.CompanyId,
                Description = description,
                Created = DateTimeOffset.Now,
                LastUpdated = DateTimeOffset.Now,
            };

            databaseContext.PageImageAssets.Add(asset);
            await databaseContext.SaveChangesAsync();
            return asset.Id;
        }


        public async Task UpdateAsset(Guid? assetId, string title, string description)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            var asset = await databaseContext.PageImageAssets.Where(p => p.Id == assetId).FirstOrDefaultAsync();
            if (asset == null) { throw new Exception("Page does not exist"); }

            asset.Title = title;
            asset.Description = description;
            asset.LastUpdated = DateTimeOffset.Now;

            await databaseContext.SaveChangesAsync();
        }

        public async Task<Page> GetPage(Guid pageId)
        {
            var page = await databaseContext.Pages
                .AsNoTracking()
                .Include(p => p.PageType)
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
                    Recursion = p.Recursion,
                    TypeTitle = p.PageType.Title,
                }).FirstOrDefaultAsync();

            if (page == null)
            {
                throw new Exception("Page does not exist");
            }

            return page;
        }
        public async Task<PagedList<Page>> GetPages(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12)
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
                .Where(pq =>
                    pq.CompanyId == signedInUser.CompanyId &&
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
                    Recursion = p.Recursion,
                    TypeTitle = p.PageType.Title,
                })
                .ToPagedResultAsync(offset - 1, length);

            return pages;
        }

        public async Task<List<PageType>> GetPageTypes()
        {
            var pageTypes = await databaseContext.PageTypes.AsNoTracking().ToListAsync();
            return pageTypes;
        }

        public async Task<List<PageRoot>> GetPageRoots()
        {
            var pageRoots = await databaseContext.PageRoots.AsNoTracking().ToListAsync();
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
        public async Task UpdatePageContent(Guid pageId, string data)
        {
            var page = await databaseContext.Pages.Where(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page == null) { throw new Exception("Page does not exist"); }
            page.Content = data;
            page.LastUpdated = DateTime.Now;
            await databaseContext.SaveChangesAsync();
        }

        public async Task<Page> GetPageWithSlug(string slug)
        {
            var page = await databaseContext.Pages
                .AsNoTracking()
                .Include(p => p.PageType)
                .Where(pq => pq.Slug == slug)
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
                    Recursion = p.Recursion,
                    TypeTitle = p.PageType.Title,
                }).FirstOrDefaultAsync();

            if (page == null)
            {
                throw new Exception("Page does not exist");
            }

            return page;
        }

        public async Task<PagedList<PageImageAsset>> GetPageAssets(string search, int sort, int offset = 1, int length = 12)
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
                    pq.CompanyId == signedInUser.CompanyId &&
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

 
    }
}
