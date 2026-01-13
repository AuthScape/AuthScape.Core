using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthScape.IDP.Areas.Admin.Pages.System
{
    [Authorize]
    public class ErrorLogsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
