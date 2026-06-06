using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthScape.Scim.Controllers;

/// <summary>
/// SCIM 2.0 discovery endpoints required by RFC 7643 §6.
/// Public (no auth) per the spec — these describe what the SCIM endpoint supports
/// so IdPs can configure themselves correctly.
/// </summary>
[ApiController]
[Route("scim/v2")]
[AllowAnonymous]
[Produces("application/scim+json")]
public class ScimDiscoveryController : ControllerBase
{
    [HttpGet("ServiceProviderConfig")]
    public IActionResult ServiceProviderConfig()
    {
        return Ok(new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
            documentationUri = "https://datatracker.ietf.org/doc/html/rfc7643",
            patch = new { supported = true },
            bulk = new { supported = false, maxOperations = 0, maxPayloadSize = 0 },
            filter = new { supported = true, maxResults = 200 },
            changePassword = new { supported = false },
            sort = new { supported = false },
            etag = new { supported = false },
            authenticationSchemes = new[]
            {
                new
                {
                    type = "oauthbearertoken",
                    name = "OAuth Bearer Token",
                    description = "Per-tenant client_credentials token from AuthScape's OpenIddict /connect/token endpoint",
                    primary = true
                }
            }
        });
    }

    [HttpGet("ResourceTypes")]
    public IActionResult ResourceTypes()
    {
        return Ok(new[]
        {
            new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ResourceType" },
                id = "User",
                name = "User",
                endpoint = "/Users",
                description = "User Account",
                schema = "urn:ietf:params:scim:schemas:core:2.0:User"
            }
        });
    }

    [HttpGet("Schemas")]
    public IActionResult Schemas()
    {
        // Minimal — full schema attribute lists are large; most IdPs accept the empty array
        // and use ResourceTypes plus runtime probing instead.
        return Ok(new[]
        {
            new
            {
                id = "urn:ietf:params:scim:schemas:core:2.0:User",
                name = "User",
                description = "User Account",
                attributes = Array.Empty<object>()
            }
        });
    }
}
