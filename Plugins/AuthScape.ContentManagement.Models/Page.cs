using System;

namespace AuthScape.ContentManagement.Models
{
    public class Page
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public PageType PageType { get; set; }
        public string? Slug { get; set; }
        public string? MetaDescription { get; set; }
        public string? HtmlData { get; set; }
        public string? CssData { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
        public long? CompanyId { get; set; }
    }

    public class PageTemplate
    {

    }

    public enum PageType
    {
        WebPage = 1,
        Email = 2
    }
}