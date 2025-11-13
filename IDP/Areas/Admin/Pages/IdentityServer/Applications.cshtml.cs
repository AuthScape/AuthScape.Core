using IDP.Models.IdentityServer;
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
    public class ApplicationsModel : PageModel
    {
        private readonly IIdentityServerService identityServerService;

        public ApplicationsModel(IIdentityServerService identityServerService)
        {
            this.identityServerService = identityServerService;
        }

        public List<ApplicationDetailsDto> Applications { get; set; } = new();

        public async Task OnGetAsync()
        {
            Applications = await identityServerService.GetAllApplicationsAsync();
        }
    }
}
