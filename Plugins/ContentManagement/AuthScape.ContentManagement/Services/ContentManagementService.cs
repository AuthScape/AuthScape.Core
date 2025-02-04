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
        Task<PagedList<PageTemplate>> GetPageTemplates(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12);
        Task<List<PageTemplate>> GetPageTemplateSelector();
        Task<List<PageType>> GetPageTypes();
        Task<Page> GetPage(Guid pageId);
        Task<PageTemplate> GetPageTemplate(long templateId);
        Task<Guid> CreateNewPage(string title, long templateId, string description);
        Task<long> CreateNewTemplate(string title, long pageTypeId, string description);
        Task UpdatePage(Guid pageId, string data);
        Task UpdatePageTemplate(long templateId, string config, string data);
        Task RemovePage(Guid pageId);
        Task ArchivePageTemplate(long templateId);
        Task RestorePageTemplate(long templateId);
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
        public async Task ArchivePageTemplate(long templateId)
        {
            var template = await databaseContext.PageTemplates.Where(pt => pt.Id == templateId).FirstOrDefaultAsync();
            if (template == null) { throw new Exception("Template does not exist"); }
            template.Archived = DateTimeOffset.Now;
            await databaseContext.SaveChangesAsync();
        }
        public async Task<Guid> CreateNewPage(string title, long templateId, string description)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }
            var slug = slugService.GenerateSlug(title);
            var template = await databaseContext.PageTemplates.AsNoTracking().Where(t => t.Id == templateId).FirstOrDefaultAsync();
            var page = new Page            { Title = title,
                PageTemplateId = templateId,
                CompanyId = signedInUser.CompanyId, 
                Description = description ,
                Slug = slug, 
                Created = DateTimeOffset.Now, 
                LastUpdated = DateTimeOffset.Now,
                Content = template.Content,
            };

            databaseContext.Pages.Add(page);
            await databaseContext.SaveChangesAsync();

            return page.Id;
        }
        public async Task<long> CreateNewTemplate(string title, long pageTypeId, string description)
        {
            throw new NotImplementedException();
        }
        public async Task<Page> GetPage(Guid pageId)
        {
            var page = await databaseContext.Pages
                .AsNoTracking()
                .Include(p => p.PageTemplate) // Ensure PageTemplate is loaded
                .Where(pq => pq.Id == pageId)
                .Select(p => new Page
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Content = p.Content,
                    CompanyId = p.CompanyId,
                    PageTemplateId = p.PageTemplateId,
                    Description = p.Description,
                    Created = p.Created,
                    LastUpdated = p.LastUpdated,

                    PageTemplate = new PageTemplate
                    {
                        Id = p.PageTemplate.Id,
                        Title = p.PageTemplate.Title,
                        PageTypeId = p.PageTemplate.PageTypeId,
                        Config = p.PageTemplate.Config,
                        Content = p.PageTemplate.Content,
                        Description = p.PageTemplate.Description,
                        Created = p.PageTemplate.Created,
                        LastUpdated = p.PageTemplate.LastUpdated
                    }
                })
                .FirstOrDefaultAsync();

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
                .Include(p => p.PageTemplate) 
                .ThenInclude(pt => pt.PageType) 
                .Where(pq =>
                    pq.CompanyId == signedInUser.CompanyId &&
                    (string.IsNullOrWhiteSpace(search) || pq.Title.ToLower().Contains(search)));

            if (chipFilters != null && chipFilters.Length > 0)
            {
                pageQuery = pageQuery.Where(pq => chipFilters.Contains(pq.PageTemplate.PageTypeId));
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
                    PageTemplateId = p.PageTemplateId,
                    Description = p.Description,
                    Created = p.Created,
                    LastUpdated = p.LastUpdated,
                    TemplateTitle = p.PageTemplate.Title,
                    TypeTitle = p.PageTemplate.PageType.Title 
                })
                .ToPagedResultAsync(offset - 1, length);

            return pages;
        }
        public async Task<PageTemplate> GetPageTemplate(long templateId)
        {
            var template = await databaseContext.PageTemplates.AsNoTracking().Where(pt => pt.Id == templateId).FirstOrDefaultAsync();
            if (template == null) { throw new Exception("Page does not exist"); }
            return template;
        }
        public async Task<PagedList<PageTemplate>> GetPageTemplates(string search, int sort, long[]? chipFilters, int offset = 1, int length = 12)
        {
            var signedInUser = await userService.GetSignedInUser();
            if (signedInUser == null) { throw new Exception("User is not logged in"); }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
            }

            var templateQuery = databaseContext.PageTemplates.AsNoTracking()
                .Where(pq =>
                    (string.IsNullOrWhiteSpace(search) || pq.Title.ToLower().Contains(search))
                );

            if (chipFilters != null && chipFilters.Length > 0)
            {
                templateQuery = templateQuery.Where(pq => chipFilters.Contains(pq.PageTypeId));
            }

            switch (sort)
            {
                case 1:
                    templateQuery = templateQuery.OrderBy(pq => pq.Title);
                    break;
                case 2:
                    templateQuery = templateQuery.OrderByDescending(pq => pq.Title);
                    break;
                case 3:
                    templateQuery = templateQuery.OrderBy(pq => pq.LastUpdated);
                    break;
                case 4:
                    templateQuery = templateQuery.OrderByDescending(pq => pq.LastUpdated);
                    break;
            }

            var templates = await templateQuery.ToPagedResultAsync(offset - 1, length);

            return templates;
        }
        public async Task<List<PageTemplate>> GetPageTemplateSelector()
        {
            var pageTemplates = await databaseContext.PageTemplates.Include(p => p.PageType).AsNoTracking().Where(pt => pt.Archived == null).Select(pt => new PageTemplate
            {
                Id = pt.Id,
                Title = pt.Title,
                PageTypeId = pt.PageTypeId,
                TypeTitle = pt.PageType.Title,
            }).ToListAsync();
            return pageTemplates;
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
        public async Task RestorePageTemplate(long templateId)
        {
            var template = await databaseContext.PageTemplates.Where(pt => pt.Id == templateId).FirstOrDefaultAsync();
            if (template == null) { throw new Exception("Template does not exist"); }
            template.Archived = null;
            await databaseContext.SaveChangesAsync();
        }
        public async Task UpdatePage(Guid pageId, string data)
        {
            var page = await databaseContext.Pages.Where(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page == null) { throw new Exception("Page does not exist"); }
            page.Content = data;
            page.LastUpdated = DateTime.Now;
            await databaseContext.SaveChangesAsync();
        }
        public async Task UpdatePageTemplate(long templateId, string config, string data)
        {
            var template = await databaseContext.PageTemplates.Where(pt => pt.Id == templateId).FirstOrDefaultAsync();
            if (template == null) { throw new Exception("Page does not exist"); }
            template.Config = data;
            template.Content = data;
            template.LastUpdated = DateTime.Now;
            await databaseContext.SaveChangesAsync();
        }
    }
}
