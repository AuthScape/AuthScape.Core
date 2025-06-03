namespace AuthScape.ContentManagement.Models
{
    public class GetPageWithSlugParam
    {
        public List<string>? Slugs { get; set; }
        public string? Host { get; set; }
    }
}