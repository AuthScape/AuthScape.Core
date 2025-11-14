using AuthScape.Models.Users;
using IDP.Models.IdentityServer;
using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class ApplicationsModel : PageModel
    {
        private readonly IIdentityServerService identityServerService;
        private readonly UserManager<AppUser> userManager;
        private readonly SignInManager<AppUser> signInManager;

        public ApplicationsModel(
            IIdentityServerService identityServerService,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            this.identityServerService = identityServerService;
            this.userManager = userManager;
            this.signInManager = signInManager;
        }

        public List<ApplicationDetailsDto> Applications { get; set; } = new();

        public async Task OnGetAsync()
        {
            Applications = await identityServerService.GetAllApplicationsAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id, string password)
        {
            try
            {
                // Verify user's password
                var user = await userManager.GetUserAsync(User);
                if (user == null)
                {
                    return new JsonResult(new { success = false, error = "User not found" }) { StatusCode = 401 };
                }

                var passwordValid = await userManager.CheckPasswordAsync(user, password);
                if (!passwordValid)
                {
                    return new JsonResult(new { success = false, error = "Invalid password" }) { StatusCode = 403 };
                }

                // Delete the application
                await identityServerService.DeleteApplicationAsync(id);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = ex.Message }) { StatusCode = 500 };
            }
        }
    }
}
