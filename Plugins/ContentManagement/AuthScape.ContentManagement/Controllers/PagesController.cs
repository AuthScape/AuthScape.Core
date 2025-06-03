using AuthScape.ContentManagement.Models;
using AuthScape.ContentManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthScape.DocumentReader.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class PagesController : ControllerBase
    {
        readonly IContentManagementService _contentManagementService;
        public PagesController(IContentManagementService contentManagementService)
        {
            _contentManagementService = contentManagementService;
        }

        [HttpPost]
        public async Task<IActionResult> GetPageWithSlug(GetPageWithSlugParam param)
        {
            var page = await _contentManagementService.GetPageWithSlug(param.Slugs, param.Host);
            return Ok(page);
        }
    }
}
