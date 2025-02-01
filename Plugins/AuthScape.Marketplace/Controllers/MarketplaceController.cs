using AuthScape.Marketplace.Models;
using AuthScape.Marketplace.Models.CSVReader;
using AuthScape.Marketplace.Services;
using Microsoft.AspNetCore.Http;
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
        public IActionResult Search(SearchParams searchParams)
        {
            var results = marketplaceService.SearchProducts(searchParams);
            return Ok(results);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await marketplaceService.Generate();

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            await marketplaceService.UploadInventory<ProductTest>(file.OpenReadStream());
            return Ok();
        }
    }
}