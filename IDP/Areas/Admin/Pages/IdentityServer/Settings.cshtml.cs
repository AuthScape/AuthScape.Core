using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class SettingsModel : PageModel
    {
        private readonly ISettingsService settingsService;

        public SettingsModel(ISettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        public List<AuthScape.Models.Settings.Settings> Settings { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Settings = await settingsService.GetAllSettingsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleAsync(string name, string currentValue)
        {
            try
            {
                var newValue = (currentValue == "1" || currentValue.Equals("true", System.StringComparison.OrdinalIgnoreCase)) ? "0" : "1";
                await settingsService.UpdateSettingAsync(name, newValue);
                SuccessMessage = $"Setting '{name}' updated successfully";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Error updating setting: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(string name, string value)
        {
            try
            {
                await settingsService.UpdateSettingAsync(name, value);
                SuccessMessage = $"Setting '{name}' updated successfully";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Error updating setting: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name, string value, int settingTypeId)
        {
            try
            {
                await settingsService.CreateSettingAsync(name, value, settingTypeId);
                SuccessMessage = $"Setting '{name}' created successfully";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Error creating setting: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string name)
        {
            try
            {
                await settingsService.DeleteSettingAsync(name);
                SuccessMessage = $"Setting '{name}' deleted successfully";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Error deleting setting: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
