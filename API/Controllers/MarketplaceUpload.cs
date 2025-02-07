using AuthScape.Marketplace.Models.CSVReader;
using AuthScape.Marketplace.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketplaceUpload : ControllerBase
    {
        readonly IMarketplaceService marketplaceService;
        public MarketplaceUpload(IMarketplaceService marketplaceService)
        {
            this.marketplaceService = marketplaceService;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            await marketplaceService.UploadCardsFile<ProductTest>(file.OpenReadStream());
            return Ok();
        }
    }
}