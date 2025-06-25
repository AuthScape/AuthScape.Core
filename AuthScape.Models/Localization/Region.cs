namespace AuthScape.Models.Localization
{
    public class Region
    {
        public Guid Id { get; set; }
        public string CultureCode { get; set; }
        public string EnglishName { get; set; }
        public string NativeName { get; set; }
        public string DisplayName { get; set; }
        public bool IsNeutralCulture { get; set; }
    }
}