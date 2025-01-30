using System;

namespace AuthScape.ContentManagement.Models
{
    public class Page
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string? Slug { get; set; }
        public string? Content { get; set; }
        public long? CompanyId { get; set; }
        public long PageTemplateId { get; set; }
        public string Description { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
        public PageTemplate PageTemplate { get; set; }  
    }

    public class PageTemplate
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public long PageTypeId { get; set; }  
        public string Config {  get; set; }
        public string Content { get; set; }
        public string Description { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
        public DateTimeOffset? Archived { get; set; }
        public PageType PageType { get; set; }
        public ICollection<Page> Pages { get; set; }
    }

    public class PageType
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public ICollection<PageTemplate> PageTemplates { get; set; }
    }
}