namespace AuthScape.Models.Sitemap
{
    public interface ISitemapService
    {
        Task<List<ISitemapItem>> Generate(string domain);
    }
}
