using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.Controllers
{
    /// <summary>
    /// Provider-agnostic <c>[Authorize]</c> for AuthScape endpoints. Accepts a valid principal from
    /// any of the auth pipelines AuthScape supports out of the box, so the same attribute keeps
    /// working when the host swaps OpenIddict for Keycloak (or adds another provider later).
    /// <para>Schemes accepted (first match wins):</para>
    /// <list type="bullet">
    ///   <item><c>OpenIddict.Validation.AspNetCore</c> — OpenIddict introspection + Keycloak introspection.</item>
    ///   <item><c>Bearer</c> (JwtBearer) — Keycloak local JWT validation via JWKS.</item>
    ///   <item><c>Identity.Application</c> — cookie-based sign-in from the IDP Razor pages.</item>
    /// </list>
    /// <para>Usage:</para>
    /// <code>
    /// [AuthScapeAuthorize]                          // any signed-in user
    /// [AuthScapeAuthorize(Roles = "Admin")]         // role gated
    /// [AuthScapeAuthorize(Policy = "MyPolicy")]     // policy gated
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AuthScapeAuthorizeAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// Comma-separated scheme list. Exposed as a constant so non-attribute call sites
        /// (e.g. <c>services.AddAuthorization</c> policies) can reuse the exact same combination.
        /// </summary>
        public const string SchemeList =
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme +
            "," + JwtBearerDefaults.AuthenticationScheme +
            ",Identity.Application";

        public AuthScapeAuthorizeAttribute()
        {
            AuthenticationSchemes = SchemeList;
        }
    }
}
