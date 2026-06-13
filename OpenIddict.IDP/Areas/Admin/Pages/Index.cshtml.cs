using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class IndexModel : PageModel
    {
        private readonly IIdentityServerService identityServerService;

        public IndexModel(IIdentityServerService identityServerService)
        {
            this.identityServerService = identityServerService;
        }

        public int ApplicationCount { get; set; }
        public int ScopeCount { get; set; }

        public async Task OnGetAsync()
        {
            var applications = await identityServerService.GetAllApplicationsAsync();
            var scopes = await identityServerService.GetAllScopesAsync();

            ApplicationCount = applications?.Count ?? 0;
            ScopeCount = scopes?.Count ?? 0;
        }
    }
}
