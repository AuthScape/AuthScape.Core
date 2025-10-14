using AuthScape.Models.Sitemap;
using System.Web.Mvc;

namespace Authscape.Sitemap
{
    public class SitemapService
    {
        [HttpGet("v1/pages")]
        public async Task<IActionResult<SitemapResponse>> GetSitemapPages()
        {
            var pages = await _context.Pages
                .Where(p => p.IsPublished)
                .Select(p => new SitemapPage
                {
                    Slug = p.Slug,
                    UpdatedAt = p.UpdatedAt,
                    Changefreq = p.Changefreq ?? "weekly",
                    Priority = p.Priority ?? "0.7"
                })
                .ToListAsync();

            return new SitemapResponse { Pages = pages };
        }
    }
}
