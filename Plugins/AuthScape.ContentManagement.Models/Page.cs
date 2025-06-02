using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.ContentManagement.Models
{
    public class Page
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string? Content { get; set; }
        public long? CompanyId { get; set; }
        public string Description { get; set; }
        public long PageTypeId { get; set; }
        public long? PageRootId { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public int? Recursion { get; set; }
        public PageType PageType { get; set; }
        public int? Order { get; set; }
        public PageRoot PageRoot { get; set; }
        public bool Highlight { get; set; }
        public bool IsContainedButton { get; set; }
        [NotMapped]
        public string TypeTitle { get; set; }

        [NotMapped]
        public string RootUrl { get; set; }
    }
    public class PageType
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public bool IsHomepage { get; set; }
        public bool IsLink { get; set; }
        public bool IsEmail { get; set; }
        public ICollection<Page> Pages { get; set; }
    }

    public class PageRoot
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string RootUrl { get; set; }
        public long? CompanyId { get; set; }
        public bool IsInHeaderNavigation { get; set; }
        public bool Highlight { get; set; }
        public int? Order { get; set; }
        public ICollection<Page> Pages { get; set; }
    }

    public class PageImageAsset
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public long? CompanyId { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }
}