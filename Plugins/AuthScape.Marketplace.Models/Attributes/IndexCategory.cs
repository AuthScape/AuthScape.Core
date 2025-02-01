namespace AuthScape.Marketplace.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexCategory : Attribute
    {
        public string CategoryName { get; }
        public IndexCategory(string categoryName)
        {
            CategoryName = categoryName;
        }
    }
}
