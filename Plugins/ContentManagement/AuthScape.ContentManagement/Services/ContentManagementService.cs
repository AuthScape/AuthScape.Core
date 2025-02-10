using AuthScape.ContentManagement.Models;
using AuthScape.DocumentReader.Controllers;
using AuthScape.Services;
using CoreBackpack;
using CoreBackpack.Services;
using CoreBackpack.Time;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System.Text;

namespace AuthScape.ContentManagement.Services
{
    public interface IContentManagementService
    {
        Task<PagedList<Page>> GetPages(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12);
        Task<List<PageType>> GetPageTypes();
        Task<Page> GetPage(Guid pageId);
        Task<Guid> CreateNewPage(string title, long pageTypeId, string description, int? recursion);
        Task UpdatePageContent(Guid pageId, string data);
        Task UpdatePage(Guid? pageId, string title, long pageTypeId, string description, int? recursion);
        Task RemovePage(Guid pageId);
    }
    public class ContentManagementService : IContentManagementService
    {
        readonly DatabaseContext databaseContext;
        readonly IUserManagementService userService;
        readonly ISlugService slugService;
        public ContentManagementService(DatabaseContext databaseContext, IUserManagementService userService, ISlugService slugService)
        {
            this.databaseContext = databaseContext;
            this.userService = userService;
            this.slugService = slugService;
        }
        public async Task UpdatePage(Guid? pageId, string title, long pageTypeId, string description, int? recursion)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (pageId == null) { throw new Exception("Id must be provided"); }

            var page = await databaseContext.Pages.Where(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page == null) { throw new Exception("Page does not exist"); }

            var slug = slugService.GenerateSlug(title);
            
            page.Title = title;
            page.PageTypeId = pageTypeId;
            page.Description = description;
            page.LastUpdated = DateTimeOffset.Now;
            page.Slug = slug;
            page.PageTypeId = pageTypeId;
            page.Recursion = recursion;

            await databaseContext.SaveChangesAsync();
        }
        public async Task<Guid> CreateNewPage(string title, long pageTypeId, string description, int? recursion)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }
            
            var slug = slugService.GenerateSlug(title);
            
            var page = new Page          
            {   Title = title,
                CompanyId = signedInUser.CompanyId, 
                Description = description,
                Slug = slug, 
                Created = DateTimeOffset.Now, 
                LastUpdated = DateTimeOffset.Now,
                PageTypeId = pageTypeId,
                Recursion = recursion
            };

            databaseContext.Pages.Add(page);
            await databaseContext.SaveChangesAsync();
            return page.Id;
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
        public async Task RemovePage(Guid pageId)
        {
            var page = await databaseContext.Pages.Where(pt => pt.Id == pageId).FirstOrDefaultAsync();
            if (page == null) { throw new Exception("Page does not exist"); }
            databaseContext.Pages.Remove(page);
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
    }
}
