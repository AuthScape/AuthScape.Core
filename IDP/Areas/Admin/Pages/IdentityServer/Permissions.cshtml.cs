using AuthScape.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class PermissionsModel : PageModel
    {
        private readonly IPermissionService permissionService;

        public PermissionsModel(IPermissionService permissionService)
        {
            this.permissionService = permissionService;
        }

        public List<PermissionDto> Permissions { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Permissions = await permissionService.GetAllPermissionsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Permission name is required";
                return RedirectToPage();
            }

            if (await permissionService.PermissionExistsAsync(name))
            {
                ErrorMessage = $"Permission '{name}' already exists";
                return RedirectToPage();
            }

            var dto = new CreatePermissionDto { Name = name };
            var success = await permissionService.CreatePermissionAsync(dto);

            if (success)
            {
                SuccessMessage = $"Permission '{name}' created successfully";
            }
            else
            {
                ErrorMessage = "Failed to create permission";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(Guid id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Permission name is required";
                return RedirectToPage();
            }

            if (await permissionService.PermissionExistsAsync(name, id))
            {
                ErrorMessage = $"Permission '{name}' already exists";
                return RedirectToPage();
            }

            var dto = new UpdatePermissionDto { Id = id, Name = name };
            var success = await permissionService.UpdatePermissionAsync(dto);

            if (success)
            {
                SuccessMessage = $"Permission '{name}' updated successfully";
            }
            else
            {
                ErrorMessage = "Failed to update permission";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var permission = await permissionService.GetPermissionByIdAsync(id);
            if (permission == null)
            {
                ErrorMessage = "Permission not found";
                return RedirectToPage();
            }

            var success = await permissionService.DeletePermissionAsync(id);

            if (success)
            {
                SuccessMessage = $"Permission '{permission.Name}' deleted successfully";
            }
            else
            {
                ErrorMessage = "Failed to delete permission";
            }

            return RedirectToPage();
        }
    }
}
