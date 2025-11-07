using AuthScape.Models.Sitemap;
using Services.Context;

namespace Services
{
    public class SitemapService : ISitemapService
    {
        public DatabaseContext DatabaseContext;
        public SitemapService(DatabaseContext databaseContext)
        {
            DatabaseContext = databaseContext;
        }

        public async Task<List<ISitemapItem>> Generate(string domain)
        {
            var sitemapData = new List<ISitemapItem>();


            sitemapData.Add(new SitemapItem("/weee", DateTime.Now, SitemapChangeFrequency.Daily, 1));


            return sitemapData;
        }
    }
}
