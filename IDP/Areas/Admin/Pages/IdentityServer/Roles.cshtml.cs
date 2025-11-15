using AuthScape.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class RolesModel : PageModel
    {
        private readonly IRoleService roleService;

        public RolesModel(IRoleService roleService)
        {
            this.roleService = roleService;
        }

        public List<RoleDto> Roles { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Roles = await roleService.GetAllRolesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Role name is required";
                return RedirectToPage();
            }

            if (await roleService.RoleExistsAsync(name))
            {
                ErrorMessage = $"Role '{name}' already exists";
                return RedirectToPage();
            }

            var dto = new CreateRoleDto { Name = name };
            var success = await roleService.CreateRoleAsync(dto);

            if (success)
            {
                SuccessMessage = $"Role '{name}' created successfully";
            }
            else
            {
                ErrorMessage = "Failed to create role";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(long id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Role name is required";
                return RedirectToPage();
            }

            if (await roleService.RoleExistsAsync(name, id))
            {
                ErrorMessage = $"Role '{name}' already exists";
                return RedirectToPage();
            }

            var dto = new UpdateRoleDto { Id = id, Name = name };
            var success = await roleService.UpdateRoleAsync(dto);

            if (success)
            {
                SuccessMessage = $"Role '{name}' updated successfully";
            }
            else
            {
                ErrorMessage = "Failed to update role";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            var role = await roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                ErrorMessage = "Role not found";
                return RedirectToPage();
            }

            if (role.UserCount > 0)
            {
                ErrorMessage = $"Cannot delete role '{role.Name}' because it has {role.UserCount} user(s) assigned";
                return RedirectToPage();
            }

            var success = await roleService.DeleteRoleAsync(id);

            if (success)
            {
                SuccessMessage = $"Role '{role.Name}' deleted successfully";
            }
            else
            {
                ErrorMessage = "Failed to delete role";
            }

            return RedirectToPage();
        }
    }
}
