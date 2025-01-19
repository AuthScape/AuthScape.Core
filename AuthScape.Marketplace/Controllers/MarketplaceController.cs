using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Services;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await marketplaceService.IndexProducts();
            return Ok();
        }
    }
}