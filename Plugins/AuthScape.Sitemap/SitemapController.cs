using AuthScape.Models.Sitemap;
using AuthScape.Sitemap.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthScape.Sitemap
{
    [Route("api/[controller]")]
    [ApiController]
    public class SitemapController : AuthScapeSitemapGenerator
    {
        readonly ISitemapService sitemapService;
        public SitemapController(ISitemapService sitemapService)
        {
            this.sitemapService = sitemapService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string domain)
        {
            var sitemap = await sitemapService.Generate(domain);
            var response = GenerateSiteMap(domain, sitemap);

            return Ok("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + "\n" + response.ToString());
        }
    }
}
