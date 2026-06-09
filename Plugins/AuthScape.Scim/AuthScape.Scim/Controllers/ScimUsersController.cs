using AuthScape.Scim.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AuthScape.Scim.Controllers;

/// <summary>
/// SCIM 2.0 Users endpoint. Authenticated via OpenIddict bearer tokens issued from
/// per-tenant client_credentials clients; the tenant is identified by the `scim_company_id` claim.
/// </summary>
[ApiController]
[Route("scim/v2/Users")]
[Authorize(AuthenticationSchemes = "Bearer", Policy = "ScimAccess")]
[Produces("application/scim+json")]
public class ScimUsersController : ControllerBase
{
    private readonly IScimService scim;
    private readonly ILogger<ScimUsersController> logger;

    public ScimUsersController(IScimService scim, ILogger<ScimUsersController> logger)
    {
        this.scim = scim;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? filter,
        [FromQuery] int? startIndex,
        [FromQuery] int? count,
        CancellationToken cancellationToken)
    {
        var companyId = ResolveCompanyId();
        if (companyId == null) return ScimError(403, "Bearer token missing scim_company_id claim");

        try
        {
            var result = await scim.ListUsersAsync(companyId.Value,
                new ScimQuery
                {
                    Filter = filter,
                    StartIndex = startIndex ?? 1,
                    Count = count ?? 100
                }, cancellationToken);
            return Ok(result);
        }
        catch (Filters.ScimFilterException ex)
        {
            return ScimError(400, ex.Message, "invalidFilter");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var companyId = ResolveCompanyId();
        if (companyId == null) return ScimError(403, "Bearer token missing scim_company_id claim");

        var user = await scim.GetUserAsync(companyId.Value, id, cancellationToken);
        return user == null ? ScimError(404, "Resource not found") : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScimUser user, CancellationToken cancellationToken)
    {
        var companyId = ResolveCompanyId();
        if (companyId == null) return ScimError(403, "Bearer token missing scim_company_id claim");

        try
        {
            var created = await scim.CreateUserAsync(companyId.Value, user, cancellationToken);
            return Created($"/scim/v2/Users/{created.Id}", created);
        }
        catch (ScimConflictException ex)
        {
            return ScimError(409, ex.Message, "uniqueness");
        }
        catch (InvalidOperationException ex)
        {
            return ScimError(400, ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Replace(string id, [FromBody] ScimUser user, CancellationToken cancellationToken)
    {
        var companyId = ResolveCompanyId();
        if (companyId == null) return ScimError(403, "Bearer token missing scim_company_id claim");

        try
        {
            var updated = await scim.ReplaceUserAsync(companyId.Value, id, user, cancellationToken);
            return Ok(updated);
        }
        catch (ScimNotFoundException)
        {
            return ScimError(404, "Resource not found");
        }
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id, [FromBody] ScimPatchRequest patch, CancellationToken cancellationToken)
    {
        var companyId = ResolveCompanyId();
        if (companyId == null) return ScimError(403, "Bearer token missing scim_company_id claim");

        try
        {
            var updated = await scim.PatchUserAsync(companyId.Value, id, patch, cancellationToken);
            return Ok(updated);
        }
        catch (ScimNotFoundException)
        {
            return ScimError(404, "Resource not found");
        }
        catch (InvalidOperationException ex)
        {
            return ScimError(400, ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var companyId = ResolveCompanyId();
        if (companyId == null) return ScimError(403, "Bearer token missing scim_company_id claim");

        var deleted = await scim.DeleteUserAsync(companyId.Value, id, cancellationToken);
        return deleted ? NoContent() : ScimError(404, "Resource not found");
    }

    private long? ResolveCompanyId()
    {
        var claim = User.FindFirstValue("scim_company_id");
        return long.TryParse(claim, out var v) ? v : null;
    }

    private IActionResult ScimError(int status, string detail, string? scimType = null)
    {
        var error = new ScimError
        {
            Status = status.ToString(),
            ScimType = scimType,
            Detail = detail
        };
        return StatusCode(status, error);
    }
}
