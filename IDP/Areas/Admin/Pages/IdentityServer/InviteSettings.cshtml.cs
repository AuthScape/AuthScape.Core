using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthScape.Models.Invite;
using AuthScape.Services.Invite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Services.Context;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class InviteSettingsModel : PageModel
    {
        private readonly IInviteSettingsService _inviteSettingsService;
        private readonly DatabaseContext _context;

        public InviteSettingsModel(IInviteSettingsService inviteSettingsService, DatabaseContext context)
        {
            _inviteSettingsService = inviteSettingsService;
            _context = context;
        }

        public InviteSettingsDto GlobalSettings { get; set; } = new();
        public List<InviteSettingsDto> CompanyOverrides { get; set; } = new();
        public List<CompanyListItem> Companies { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            GlobalSettings = await _inviteSettingsService.GetGlobalSettingsAsync();
            CompanyOverrides = await _inviteSettingsService.GetAllCompanyOverridesAsync();
            Companies = await _context.Companies
                .Where(c => !c.IsDeactivated)
                .OrderBy(c => c.Title)
                .Select(c => new CompanyListItem { Id = c.Id, Title = c.Title })
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateGlobalAsync(
            bool enableInviteToCompany,
            bool enableInviteToLocation,
            bool allowSettingPermissions,
            bool allowSettingRoles,
            bool enforceSamePermissions,
            bool enforceSameRole)
        {
            var dto = new UpdateInviteSettingsDto
            {
                CompanyId = null,
                EnableInviteToCompany = enableInviteToCompany,
                EnableInviteToLocation = enableInviteToLocation,
                AllowSettingPermissions = allowSettingPermissions,
                AllowSettingRoles = allowSettingRoles,
                EnforceSamePermissions = enforceSamePermissions,
                EnforceSameRole = enforceSameRole
            };

            await _inviteSettingsService.SaveGlobalSettingsAsync(dto);
            SuccessMessage = "Global invite settings updated successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateOverrideAsync(
            long companyId,
            bool enableInviteToCompany,
            bool enableInviteToLocation,
            bool allowSettingPermissions,
            bool allowSettingRoles,
            bool enforceSamePermissions,
            bool enforceSameRole)
        {
            if (companyId <= 0)
            {
                ErrorMessage = "Please select a company";
                return RedirectToPage();
            }

            var existing = await _inviteSettingsService.GetCompanySettingsAsync(companyId);
            if (existing != null)
            {
                ErrorMessage = "An override for this company already exists";
                return RedirectToPage();
            }

            var dto = new UpdateInviteSettingsDto
            {
                CompanyId = companyId,
                EnableInviteToCompany = enableInviteToCompany,
                EnableInviteToLocation = enableInviteToLocation,
                AllowSettingPermissions = allowSettingPermissions,
                AllowSettingRoles = allowSettingRoles,
                EnforceSamePermissions = enforceSamePermissions,
                EnforceSameRole = enforceSameRole
            };

            await _inviteSettingsService.SaveCompanySettingsAsync(dto);
            SuccessMessage = "Company override created successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateOverrideAsync(
            long companyId,
            bool enableInviteToCompany,
            bool enableInviteToLocation,
            bool allowSettingPermissions,
            bool allowSettingRoles,
            bool enforceSamePermissions,
            bool enforceSameRole)
        {
            var dto = new UpdateInviteSettingsDto
            {
                CompanyId = companyId,
                EnableInviteToCompany = enableInviteToCompany,
                EnableInviteToLocation = enableInviteToLocation,
                AllowSettingPermissions = allowSettingPermissions,
                AllowSettingRoles = allowSettingRoles,
                EnforceSamePermissions = enforceSamePermissions,
                EnforceSameRole = enforceSameRole
            };

            await _inviteSettingsService.SaveCompanySettingsAsync(dto);
            SuccessMessage = "Company override updated successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteOverrideAsync(long companyId)
        {
            await _inviteSettingsService.DeleteCompanyOverrideAsync(companyId);
            SuccessMessage = "Company override removed successfully";
            return RedirectToPage();
        }
    }

    public class CompanyListItem
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }
}
