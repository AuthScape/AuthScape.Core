using AuthScape.ContentManagement.Models;

namespace AuthScape.ContentManagement.Models
{
    public class PageSummary
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public PageType PageType { get; set; }
        public string Slug { get; set; }
        public string Created { get; set; }
        public string LastUpdated { get; set; }

        public string Content { get; set; }
    }
}