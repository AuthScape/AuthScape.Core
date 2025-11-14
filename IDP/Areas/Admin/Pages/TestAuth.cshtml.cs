using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages
{
    // NOTE: No [Authorize] attribute - accessible to everyone for testing
    [Area("Admin")]
    public class TestAuthModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;

        public TestAuthModel(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public string UserId { get; set; }
        public List<string> UserRoles { get; set; }

        public async Task OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    UserId = user.Id.ToString();
                    UserRoles = (await _userManager.GetRolesAsync(user)).ToList();
                }
            }
        }
    }
}
