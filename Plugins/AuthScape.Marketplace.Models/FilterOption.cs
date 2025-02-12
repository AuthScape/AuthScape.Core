namespace AuthScape.Marketplace.Models
{
    public class FilterOption
    {
        public string Key { get; set; }
        public int Value { get; set; }

        public List<FilterOption>? Subcategories { get; set; }
    }
}
