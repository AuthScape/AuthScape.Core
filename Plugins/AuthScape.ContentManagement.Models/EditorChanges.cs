namespace AuthScape.ContentManagement.Models
{
    public class EditorChanges
    {
        public Guid PageId { get; set; }
        public string HtmlData { get; set; }
        public string CssData { get; set; }
    }
}