using System.Linq;
using AuthScape.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// Starter example controller. Delete or rename when you start adding your own endpoints —
    /// this is just here to show the two basic shapes: an anonymous endpoint, and one that requires
    /// a valid bearer token from any AuthScape-supported issuer (OpenIddict or Keycloak).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ExampleController : ControllerBase
    {
        /// <summary>
        /// Anonymous health probe. <c>GET /api/example/ping</c> returns 200 with no auth required.
        /// </summary>
        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new { status = "ok" });
        }

        /// <summary>
        /// Authenticated endpoint. <c>GET /api/example/me</c> returns the claims attached to the
        /// caller's access token. Requires a valid bearer token — <c>[AuthScapeAuthorize]</c>
        /// accepts the token regardless of whether the active issuer is OpenIddict or Keycloak.
        /// </summary>
        [HttpGet("me")]
        [AuthScapeAuthorize]
        public IActionResult Me()
        {
            return Ok(new
            {
                sub  = User.FindFirst("sub")?.Value,
                name = User.Identity?.Name,
                claims = User.Claims.Select(c => new { c.Type, c.Value }),
            });
        }
    }
}
