using AuthScape.Controllers;
using AuthScape.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AuthScapeAuthorize]
    public class UserManagementController : ControllerBase
    {
        readonly IUserManagementService userManagementService;
        public UserManagementController(IUserManagementService userManagementService)
        {
            this.userManagementService = userManagementService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(await userManagementService.GetSignedInUser());
        }
    }
}
