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

        /// <summary>
        /// Record a click on a product. Use the trackingId from the Search response.
        /// </summary>
        /// <param name="trackingId">The tracking ID returned from the Search endpoint</param>
        /// <param name="productOrServiceId">The ID of the product that was clicked</param>
        [HttpPost]
        public async Task<IActionResult> Clicked(Guid trackingId, string productOrServiceId)
        {
            await marketplaceService.Clicked(trackingId, productOrServiceId);
            return Ok();
        }

        /// <summary>
        /// Get paginated/searchable filter options for a specific category.
        /// Use this endpoint when the user clicks "Show more" on a filter category (e.g., Brands)
        /// to load additional options without overwhelming browser memory.
        /// </summary>
        /// <param name="request">Filter options request with category, search term, and pagination</param>
        /// <returns>Paginated list of filter options for the specified category</returns>
        [HttpPost]
        public async Task<IActionResult> GetFilterOptions(FilterOptionsRequest request)
        {
            var results = await marketplaceService.GetFilterOptions(request);
            return Ok(results);
        }

        /// <summary>
        /// Get analytics data including click-through rates, popular products, and search trends.
        /// </summary>
        /// <param name="platformId">Platform ID to get analytics for</param>
        /// <param name="oemCompanyId">Optional OEM company ID for private label analytics</param>
        /// <param name="days">Number of days to look back (default: 30)</param>
        [HttpGet]
        public async Task<IActionResult> Analytics(int platformId = 1, long? oemCompanyId = null, int days = 30)
        {
            var results = await marketplaceService.GetAnalytics(platformId, oemCompanyId, days);
            return Ok(results);
        }
    }
}