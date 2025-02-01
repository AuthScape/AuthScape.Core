namespace AuthScape.Marketplace.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TextSearchable : Attribute
    {
        public TextSearchable()
        {
        }
    }
}
