using AuthScape.ContentManagement.Models;
using AuthScape.ContentManagement.Services;
using CoreBackpack.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            var data = await _contentManagementService.GetPages(dataGridParam.Search, dataGridParam.Sort, dataGridParam.ChipFilters, dataGridParam.Offset, dataGridParam.Length);
            return Ok(new ReactDataTable()
            {
                recordsTotal = data.total,
                recordsFiltered = data.total,
                data = data
            });
        }

        [HttpPost]
        public async Task<IActionResult> GetPageTemplates([FromBody] DataGridParam dataGridParam)
        {
            var data = await _contentManagementService.GetPageTemplates(dataGridParam.Search, dataGridParam.Sort, dataGridParam.ChipFilters, dataGridParam.Offset, dataGridParam.Length);
            return Ok(new ReactDataTable()
            {
                recordsTotal = data.total,
                recordsFiltered = data.total,
                data = data
            });
        }
       
        [HttpGet]
        public async Task<IActionResult> GetPageTemplateSelector()
        {
            var templates = await _contentManagementService.GetPageTemplateSelector();
            return Ok(templates);
        }
       
        [HttpGet]
        public async Task<IActionResult> GetPageTypes()
        {
            var pageTypes = await _contentManagementService.GetPageTypes();
            return Ok(pageTypes);
        }
       
        [HttpGet]
        public async Task<IActionResult> GetPage(Guid pageId)
        {
            var page = await _contentManagementService.GetPage(pageId);
            return Ok(page);
        }
       
        [HttpGet]
        public async Task<IActionResult> GetPageTemplate(long templateId)
        {
            var template = await _contentManagementService.GetPageTemplate(templateId);
            return Ok(template);
        }
      
        [HttpPost]
        public async Task<IActionResult> CreateNewPage([FromBody] CreationParam param)
        {
            await _contentManagementService.CreateNewPage(param.Title, param.Id, param.Description);
            return Ok();
        }
       
        [HttpPost]
        public async Task<IActionResult> CreateNewTemplate([FromBody] CreationParam param)
        {
            await _contentManagementService.CreateNewTemplate(param.Title, param.Id, param.Description);
            return Ok();
        }
               
        [HttpPut]
        public async Task<IActionResult> UpdatePage(Guid pageId, string data)
        {
            await _contentManagementService.UpdatePage(pageId, data);
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdatePageTemplate(long templateId, string config, string data)
        {
            await _contentManagementService.UpdatePageTemplate(templateId, config, data);
            return Ok();
        }
                [HttpPost]
        public async Task<IActionResult> RemovePage(Guid pageId)
        {
            await _contentManagementService.RemovePage(pageId);
            return Ok();
        }
        [HttpPut]
        public async Task<IActionResult> ArchivePageTemplate(long templateId)
        {
            await _contentManagementService.ArchivePageTemplate(templateId);
            return Ok();
        }
        [HttpPut]
        public async Task<IActionResult> RestorePageTemplate(long templateId)
        {
            await _contentManagementService.ArchivePageTemplate(templateId);
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
    }

    public class CreationParam
    {
        public string Title { get; set; }
        public long Id { get; set; }
        public string Description { get; set; }
    }
}
