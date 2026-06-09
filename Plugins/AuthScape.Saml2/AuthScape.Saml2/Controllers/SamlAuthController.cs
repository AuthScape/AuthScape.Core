using AuthScape.AccountLinking;
using AuthScape.AccountLinking.Models;
using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthScape.Saml2.Controllers;

/// <summary>
/// REST entry point for SAML authentication. Mobile / SPA / native clients embed a WebView
/// to perform the SAML round-trip with the customer's IdP, capture the resulting SAML response,
/// and POST it here to be exchanged for an AuthScape session (and via the OAuth grant handler
/// at /connect/token, an access token).
///
/// The SAML browser ACS endpoint at /Saml2/Acs/{configId} is registered separately via the
/// Razor flow; this controller is the multi-platform path that does NOT require Razor.
/// </summary>
[ApiController]
[Route("api/saml")]
public class SamlAuthController : ControllerBase
{
    private readonly ISamlService saml;
    private readonly IAccountLinkingService linker;
    private readonly SignInManager<AppUser> signInManager;
    private readonly UserManager<AppUser> userManager;
    private readonly ILogger<SamlAuthController> logger;

    public SamlAuthController(
        ISamlService saml,
        IAccountLinkingService linker,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        ILogger<SamlAuthController> logger)
    {
        this.saml = saml;
        this.linker = linker;
        this.signInManager = signInManager;
        this.userManager = userManager;
        this.logger = logger;
    }

    public class SamlExchangeRequest
    {
        /// <summary>Base64-encoded SAML response XML, exactly as the IdP returned it to the browser.</summary>
        public string SamlResponse { get; set; } = "";
        public long ConfigId { get; set; }
        public bool CreateSessionCookie { get; set; } = false;
    }

    public class SamlExchangeResponse
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public long? AppUserId { get; set; }
        public AccountLinkOutcome? LinkOutcome { get; set; }
        public string? UserFacingMessage { get; set; }
        public string? Provider { get; set; }
        public string? ExternalUserId { get; set; }
    }

    [HttpPost("exchange")]
    public async Task<ActionResult<SamlExchangeResponse>> Exchange(
        [FromBody] SamlExchangeRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrEmpty(request.SamlResponse))
            return BadRequest(new SamlExchangeResponse { Success = false, FailureReason = "samlResponse is required" });

        var config = await saml.GetConfigAsync(request.ConfigId, cancellationToken);
        if (config == null || !config.IsEnabled)
            return NotFound(new SamlExchangeResponse { Success = false, FailureReason = "SAML configuration not found or disabled" });

        ExternalIdentity identity;
        try
        {
            identity = await saml.ValidateAssertionAsync(request.SamlResponse, request.ConfigId, cancellationToken);
        }
        catch (SamlValidationException ex)
        {
            logger.LogWarning(ex, "SAML assertion validation failed for config {ConfigId}", request.ConfigId);
            return Unauthorized(new SamlExchangeResponse { Success = false, FailureReason = ex.Message });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        var linkResult = await linker.ResolveAsync(identity, config.AccountLinkingPolicy, ip, ua, cancellationToken);

        if (linkResult.Outcome == AccountLinkOutcome.PendingManualVerification)
        {
            return Accepted(new SamlExchangeResponse
            {
                Success = false,
                LinkOutcome = linkResult.Outcome,
                UserFacingMessage = linkResult.UserFacingMessage,
                Provider = identity.Provider,
                ExternalUserId = identity.ExternalUserId
            });
        }

        if (linkResult.AppUserId == null)
            return StatusCode(500, new SamlExchangeResponse { Success = false, FailureReason = "Account linking failed unexpectedly" });

        if (request.CreateSessionCookie)
        {
            var user = await userManager.FindByIdAsync(linkResult.AppUserId.Value.ToString());
            if (user != null)
                await signInManager.SignInAsync(user, isPersistent: false);
        }

        return Ok(new SamlExchangeResponse
        {
            Success = true,
            AppUserId = linkResult.AppUserId,
            LinkOutcome = linkResult.Outcome,
            Provider = identity.Provider,
            ExternalUserId = identity.ExternalUserId
        });
    }

    /// <summary>
    /// Returns SP metadata for a given configuration. Customers paste this URL into their IdP
    /// during setup; the IdP fetches it and learns AuthScape's entity ID + ACS URL.
    /// </summary>
    [HttpGet("metadata/{configId:long}")]
    public async Task<IActionResult> Metadata(long configId, CancellationToken cancellationToken)
    {
        var config = await saml.GetConfigAsync(configId, cancellationToken);
        if (config == null || !config.IsEnabled)
            return NotFound();

        var xml = $$"""
            <?xml version="1.0"?>
            <md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata" entityID="{{System.Security.SecurityElement.Escape(config.SpEntityId)}}">
              <md:SPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol" WantAssertionsSigned="{{config.WantAssertionsSigned.ToString().ToLowerInvariant()}}">
                <md:AssertionConsumerService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
                  Location="{{System.Security.SecurityElement.Escape(config.AcsUrl)}}" index="0" isDefault="true"/>
              </md:SPSSODescriptor>
            </md:EntityDescriptor>
            """;
        return Content(xml, "application/samlmetadata+xml");
    }
}
