namespace AuthScape.Models.Sitemap
{
    public class SEOAttribute : Attribute
    {
        public SEOAttribute(SitemapChangeFrequency Frequency = SitemapChangeFrequency.Always, double Priority = 0.5)
        {
            this.Frequency = Frequency;
            this.Priority = Priority;
        }

        public SitemapChangeFrequency Frequency { get; set; }
        public double Priority { get; set; }
    }
}