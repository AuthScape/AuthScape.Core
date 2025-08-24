using AuthScape.ContentManagement.Models;
using AuthScape.ContentManagement.Services;
using CoreBackpack.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.DocumentReader.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public class ContentManagementController : ControllerBase
    {
        readonly IContentManagementService _contentManagementService;
        public ContentManagementController(IContentManagementService contentManagementService)
        {
            _contentManagementService = contentManagementService;
        }

        [HttpPost]
        public async Task<IActionResult> GetPages([FromBody] DataGridParam dataGridParam)
        {
            var data = await _contentManagementService.GetPages(dataGridParam.Search, dataGridParam.Sort, dataGridParam.ChipFilters, dataGridParam.Offset, dataGridParam.Length, dataGridParam.PrivateLabelCompanyId);
            return Ok(new ReactDataTable()
            {
                recordsTotal = data.total,
                recordsFiltered = data.total,
                data = data
            });
        }

        [HttpPost]
        public async Task<IActionResult> GetPageAssets([FromBody] DataGridParam dataGridParam)
        {
            var data = await _contentManagementService.GetPageAssets(dataGridParam.Search, dataGridParam.Sort, dataGridParam.Offset, dataGridParam.Length, dataGridParam.PrivateLabelCompanyId);
            return Ok(new ReactDataTable()
            {
                recordsTotal = data.total,
                recordsFiltered = data.total,
                data = data
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetPageTypes()
        {
            var pageTypes = await _contentManagementService.GetPageTypes();
            return Ok(pageTypes);
        }

        [HttpGet]
        public async Task<IActionResult> GetPageRoots(long? privateLabelCompanyId = null)
        {
            var pageRoots = await _contentManagementService.GetPageRoots(privateLabelCompanyId);
            return Ok(pageRoots);
        }

        [HttpGet]
        public async Task<IActionResult> GetPage(Guid pageId)
        {
            var page = await _contentManagementService.GetPage(pageId);
            return Ok(page);
        }

        [HttpPost]
        public async Task<IActionResult> GetPageWithSlug(GetPageWithSlugParam param)
        {
            var page = await _contentManagementService.GetPageWithSlug(param.Slugs, param.Host);
            return Ok(page);
        }

        [HttpGet]
        public async Task<IActionResult> GetPageImageAssets(long? oemCompanyId)
        {
            var page = await _contentManagementService.GetPageImageAssets(oemCompanyId);
            return Ok(page);
        }

        [HttpGet]
        public async Task<IActionResult> GetHomePage()
        {
            var page = await _contentManagementService.GetHomepage();
            return Ok(page);
        }

        [HttpPost]
        public async Task<IActionResult> CreateNewPage([FromBody] PageParam param)
        {
            await _contentManagementService.CreateNewPage(param.Title, param.PageTypeId, param.PageRootId, param.Description, param.Recursion, param.Slug, param.PrivateLabelCompanyId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePage([FromBody] PageParam param)
        {
            await _contentManagementService.UpdatePage(param.PageId, param.Title, param.PageTypeId, param.PageRootId, param.Description, param.Recursion, param.Slug, param.PrivateLabelCompanyId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePageContent([FromBody] ContentParam contentParam)
        {
            await _contentManagementService.UpdatePageContent(contentParam.PageId, contentParam.Content);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateNewAsset([FromForm] AssetParam param)
        {
            await _contentManagementService.CreateNewAsset(param.Title, param.File, param.Description, param.PrivateLabelCompanyId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAsset([FromBody] AssetParam param)
        {
            await _contentManagementService.UpdateAsset(param.AssetId, param.Title, param.Description, param.PrivateLabelCompanyId);
            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> RemovePage(Guid pageId)
        {
            await _contentManagementService.RemovePage(pageId);
            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> RemoveAsset(Guid assetId)
        {
            await _contentManagementService.RemoveAsset(assetId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePageDuplication(Guid pageId, long oemCompanyId)
        {
            await _contentManagementService.CreatePageDuplication(pageId, oemCompanyId);
            return Ok();
        }

    }

    public class DataGridParam
    {
        public int Offset { get; set; }
        public int Length { get; set; }
        public string? Search { get; set; }
        public int Sort { get; set; }
        public long[]? ChipFilters { get; set; }
        public long? PrivateLabelCompanyId { get; set; }
    }

    public class PageParam
    {
        public Guid? PageId { get; set; }
        public string Title { get; set; }
        public long PageTypeId { get; set; }
        public long? PageRootId { get; set; }
        public string Description { get; set; }
        public int? Recursion { get; set; }
        public string Slug { get; set; }
        public long? PrivateLabelCompanyId { get; set; }
    }

    public class AssetParam
    {
        public Guid? AssetId { get; set; }
        public IFormFile? File { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public long? PrivateLabelCompanyId { get; set; }
    }

    public class ContentParam
    {
        public Guid PageId { get; set; }
        public string Content { get; set; }
    }

}
