using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.Context;

namespace AuthScape.IDP.Controllers
{
    /// <summary>
    /// Base page model for IDP Razor pages. Previously hosted private-label CSS/branding lookups
    /// against the DnsRecords table; that table belongs to the optional PrivateLabel module which
    /// is no longer part of the auth core. The hooks remain as no-ops so existing pages
    /// (Login/Register/ForgotPassword) compile — wire them back up once you re-add PrivateLabel.
    /// </summary>
    public class AuthScapePageModel : PageModel
    {
        public string? MinifiedCSS { get; set; }
        public string? CompanyName { get; set; }
        public long? CompanyId { get; set; }
        public string? CompanyLogo { get; set; }

        public AuthScapePageModel(DatabaseContext databaseContext)
        {
            // Kept for backwards-compatible signature; DatabaseContext is unused now.
        }

        public Task EnablePrivateLabelExperience(string returnUrl)
        {
            // No-op while the PrivateLabel module is disabled. Pages bind the four properties
            // above (MinifiedCSS, CompanyName, CompanyId, CompanyLogo) directly; they stay null.
            return Task.CompletedTask;
        }
    }
}
