using AuthScape.Models.Invite;
using AuthScape.Services;
using AuthScape.Services.Invite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Context;

namespace AuthScape.IDP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class InviteSettingsController : ControllerBase
    {
        private readonly IInviteSettingsService _inviteSettingsService;
        private readonly IRoleService _roleService;
        private readonly IPermissionService _permissionService;
        private readonly DatabaseContext _context;

        public InviteSettingsController(
            IInviteSettingsService inviteSettingsService,
            IRoleService roleService,
            IPermissionService permissionService,
            DatabaseContext context)
        {
            _inviteSettingsService = inviteSettingsService;
            _roleService = roleService;
            _permissionService = permissionService;
            _context = context;
        }

        /// <summary>
        /// Get global invite settings (defaults)
        /// </summary>
        [HttpGet("global")]
        public async Task<IActionResult> GetGlobalSettings()
        {
            var settings = await _inviteSettingsService.GetGlobalSettingsAsync();
            return Ok(settings);
        }

        /// <summary>
        /// Update global invite settings
        /// </summary>
        [HttpPut("global")]
        public async Task<IActionResult> UpdateGlobalSettings([FromBody] UpdateInviteSettingsDto dto)
        {
            dto.CompanyId = null; // Ensure it's global
            await _inviteSettingsService.SaveGlobalSettingsAsync(dto);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Get company-specific invite settings override
        /// </summary>
        [HttpGet("company/{companyId}")]
        public async Task<IActionResult> GetCompanySettings(long companyId)
        {
            var settings = await _inviteSettingsService.GetCompanySettingsAsync(companyId);
            return settings != null ? Ok(settings) : NotFound();
        }

        /// <summary>
        /// Create or update company-specific invite settings
        /// </summary>
        [HttpPut("company/{companyId}")]
        public async Task<IActionResult> UpdateCompanySettings(
            long companyId,
            [FromBody] UpdateInviteSettingsDto dto)
        {
            dto.CompanyId = companyId;
            await _inviteSettingsService.SaveCompanySettingsAsync(dto);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Delete company-specific settings override (will fall back to global)
        /// </summary>
        [HttpDelete("company/{companyId}")]
        public async Task<IActionResult> DeleteCompanyOverride(long companyId)
        {
            await _inviteSettingsService.DeleteCompanyOverrideAsync(companyId);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Get all company-specific overrides
        /// </summary>
        [HttpGet("overrides")]
        public async Task<IActionResult> GetAllCompanyOverrides()
        {
            var overrides = await _inviteSettingsService.GetAllCompanyOverridesAsync();
            return Ok(overrides);
        }

        /// <summary>
        /// Get effective settings for a company (company override or global default)
        /// </summary>
        [HttpGet("effective")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEffectiveSettings([FromQuery] long? companyId = null)
        {
            var settings = await _inviteSettingsService.GetEffectiveSettingsAsync(companyId);
            return Ok(settings);
        }

        /// <summary>
        /// Get all available roles for invite form
        /// </summary>
        [HttpGet("roles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableRoles()
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(roles);
        }

        /// <summary>
        /// Get all available permissions for invite form
        /// </summary>
        [HttpGet("permissions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailablePermissions()
        {
            var permissions = await _permissionService.GetAllPermissionsAsync();
            return Ok(permissions);
        }

        /// <summary>
        /// Get all companies for dropdown in admin page
        /// </summary>
        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies()
        {
            var companies = await _context.Companies
                .Where(c => !c.IsDeactivated)
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();
            return Ok(companies);
        }
    }
}
