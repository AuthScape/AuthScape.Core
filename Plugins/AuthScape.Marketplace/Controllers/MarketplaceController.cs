using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthScape.Marketplace.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class MarketplaceController : ControllerBase
    {
        readonly IMarketplaceService marketplaceService;
        public MarketplaceController(IMarketplaceService marketplaceService)
        {
            this.marketplaceService = marketplaceService;
        }

        [HttpPost]
        public async Task<IActionResult> Search(SearchParams searchParams)
        {
            var results = await marketplaceService.SearchProducts(searchParams);
            return Ok(results);
        }

        [HttpPost]
        public async Task<IActionResult> Clicked(string productOrServiceId, int platformId = 1, long? companyId = null)
        {
            await marketplaceService.Clicked(platformId, productOrServiceId, companyId);
            return Ok();
        }


        //[HttpGet]
        //public async Task<IActionResult> Index(long platformId = 1, long? companyId = null)
        //{
        //    await marketplaceService.Generate(platformId, companyId);

        //    return Ok();
        //}
    }
}