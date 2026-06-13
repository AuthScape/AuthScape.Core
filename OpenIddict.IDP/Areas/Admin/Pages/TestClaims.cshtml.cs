using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IDP.Areas.Admin.Pages
{
    [Authorize] // Only require authentication, not Admin role
    [Area("Admin")]
    public class TestClaimsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
