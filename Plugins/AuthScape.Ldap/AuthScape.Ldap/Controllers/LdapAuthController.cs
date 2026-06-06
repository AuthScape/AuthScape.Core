using AuthScape.AccountLinking;
using AuthScape.AccountLinking.Models;
using AuthScape.Ldap.Models;
using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthScape.Ldap.Controllers;

/// <summary>
/// REST entry point for LDAP authentication. This is the multi-platform path —
/// mobile / SPA / native clients post username/password here and receive either
/// a session cookie (for browser flows) or instructions for OAuth-grant exchange.
///
/// The controller is intentionally thin: it validates LDAP credentials via
/// ILdapAuthService, resolves the AppUser via IAccountLinkingService (honoring the
/// configured AccountLinkingPolicy and email-verified rules), and signs the user in.
/// Token issuance for OAuth clients goes through the LDAP password grant handler
/// at /connect/token (registered separately).
/// </summary>
[ApiController]
[Route("api/auth/ldap")]
public class LdapAuthController : ControllerBase
{
    private readonly ILdapAuthService ldap;
    private readonly IAccountLinkingService linker;
    private readonly SignInManager<AppUser> signInManager;
    private readonly UserManager<AppUser> userManager;
    private readonly ILogger<LdapAuthController> logger;

    public LdapAuthController(
        ILdapAuthService ldap,
        IAccountLinkingService linker,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        ILogger<LdapAuthController> logger)
    {
        this.ldap = ldap;
        this.linker = linker;
        this.signInManager = signInManager;
        this.userManager = userManager;
        this.logger = logger;
    }

    public class LdapAuthenticateRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>Optional — if multiple LDAP configs match the username, this picks one explicitly.</summary>
        public long? ConfigId { get; set; }
        /// <summary>Optional — restricts config resolution to a specific tenant.</summary>
        public long? CompanyId { get; set; }
        /// <summary>If true, also establishes an Identity cookie for browser flows.</summary>
        public bool CreateSessionCookie { get; set; } = false;
    }

    public class LdapAuthenticateResponse
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public long? AppUserId { get; set; }
        public AccountLinkOutcome? LinkOutcome { get; set; }
        public string? UserFacingMessage { get; set; }
        /// <summary>The (Provider, ExternalUserId) caller can use to perform downstream OAuth grants.</summary>
        public string? Provider { get; set; }
        public string? ExternalUserId { get; set; }
    }

    [HttpPost("authenticate")]
    public async Task<ActionResult<LdapAuthenticateResponse>> Authenticate(
        [FromBody] LdapAuthenticateRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new LdapAuthenticateResponse { Success = false, FailureReason = "username and password required" });

        var config = await ldap.ResolveConfigAsync(request.Username, request.CompanyId, request.ConfigId, cancellationToken);
        if (config == null)
            return NotFound(new LdapAuthenticateResponse { Success = false, FailureReason = "No LDAP configuration matches" });

        var ldapResult = await ldap.AuthenticateAsync(request.Username, request.Password, config.Id, cancellationToken);
        if (!ldapResult.Success || ldapResult.UserDistinguishedName == null)
        {
            // Don't leak which step failed (config not found vs bad creds vs server down).
            // The 401 is what mobile / SPA clients expect for "wrong credentials."
            return Unauthorized(new LdapAuthenticateResponse { Success = false, FailureReason = ldapResult.FailureReason });
        }

        var email = ldapResult.Attributes.GetValueOrDefault("email");
        var emailVerified = IsEmailFromTrustedDomain(email, config.TrustedEmailDomains);

        var providerScheme = $"Ldap_{config.Id}";
        var identity = new ExternalIdentity
        {
            Provider = providerScheme,
            ExternalUserId = ldapResult.UserDistinguishedName,
            Email = email,
            EmailVerifiedByProvider = emailVerified,
            FirstName = ldapResult.Attributes.GetValueOrDefault("firstName"),
            LastName = ldapResult.Attributes.GetValueOrDefault("lastName"),
            ProviderDisplayName = config.Name,
            CompanyId = config.CompanyId,
            RawClaims = ldapResult.Attributes
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();

        var linkResult = await linker.ResolveAsync(identity, config.AccountLinkingPolicy, ip, ua, cancellationToken);

        if (linkResult.Outcome == AccountLinkOutcome.PendingManualVerification)
        {
            return Accepted(new LdapAuthenticateResponse
            {
                Success = false,
                LinkOutcome = linkResult.Outcome,
                UserFacingMessage = linkResult.UserFacingMessage,
                Provider = providerScheme,
                ExternalUserId = ldapResult.UserDistinguishedName
            });
        }

        if (linkResult.AppUserId == null)
            return StatusCode(500, new LdapAuthenticateResponse { Success = false, FailureReason = "Account linking failed unexpectedly" });

        if (request.CreateSessionCookie)
        {
            var user = await userManager.FindByIdAsync(linkResult.AppUserId.Value.ToString());
            if (user != null)
                await signInManager.SignInAsync(user, isPersistent: false);
        }

        return Ok(new LdapAuthenticateResponse
        {
            Success = true,
            AppUserId = linkResult.AppUserId,
            LinkOutcome = linkResult.Outcome,
            Provider = providerScheme,
            ExternalUserId = ldapResult.UserDistinguishedName
        });
    }

    private static bool IsEmailFromTrustedDomain(string? email, string? trustedDomainsCsv)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(trustedDomainsCsv))
            return false;
        var atIndex = email.IndexOf('@');
        if (atIndex < 0) return false;
        var domain = email[(atIndex + 1)..];
        return trustedDomainsCsv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase));
    }
}
