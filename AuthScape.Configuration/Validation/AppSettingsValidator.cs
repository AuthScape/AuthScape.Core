using Microsoft.Extensions.Options;
using Services.Database;

namespace AuthScape.Configuration.Validation;

/// <summary>
/// Validates AppSettings configuration at startup.
/// Performs additional validation beyond data annotations.
/// </summary>
public class AppSettingsValidator : IValidateOptions<AppSettings>
{
    public ValidateOptionsResult Validate(string? name, AppSettings options)
    {
        var failures = new List<string>();

        // Required fields validation
        if (string.IsNullOrWhiteSpace(options.DatabaseContext))
        {
            failures.Add("AppSettings:DatabaseContext - Database connection string is required.");
        }

        if (string.IsNullOrWhiteSpace(options.IDPUrl))
        {
            failures.Add("AppSettings:IDPUrl - Identity Provider URL is required.");
        }
        else if (!Uri.TryCreate(options.IDPUrl, UriKind.Absolute, out var idpUri))
        {
            failures.Add("AppSettings:IDPUrl - Must be a valid absolute URL.");
        }
        else if (idpUri.Scheme != "http" && idpUri.Scheme != "https")
        {
            failures.Add("AppSettings:IDPUrl - Must use http or https scheme.");
        }

        // URL validations (if configured)
        ValidateOptionalUrl(options.WebsiteRedirectUrl, "AppSettings:WebsiteRedirectUrl", failures);
        ValidateOptionalUrl(options.InviteSignupRedirectUrl, "AppSettings:InviteSignupRedirectUrl", failures);
        ValidateOptionalUrl(options.LoginRedirectUrl, "AppSettings:LoginRedirectUrl", failures);

        // Storage validation (if configured)
        if (options.Storage != null)
        {
            ValidateOptionalUrl(options.Storage.BaseUri, "AppSettings:Storage:BaseUri", failures);
        }

        // Stripe and SendGrid validation removed with the Billing and Email modules.

        // Token lifespan validation
        if (options.DataProtectionTokenProviderOptions_TokenLifespanByDays.HasValue)
        {
            var days = options.DataProtectionTokenProviderOptions_TokenLifespanByDays.Value;
            if (days < 1 || days > 365)
            {
                failures.Add("AppSettings:DataProtectionTokenProviderOptions_TokenLifespanByDays - Must be between 1 and 365 days.");
            }
        }

        // Stage validation
        if (options.Stage < 0 || (int)options.Stage > 3)
        {
            failures.Add("AppSettings:Stage - Invalid stage value. Use 1 (Development), 2 (Staging), or 3 (Production).");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateOptionalUrl(string? url, string propertyPath, List<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                failures.Add($"{propertyPath} - Must be a valid absolute URL.");
            }
            else if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                failures.Add($"{propertyPath} - Must use http or https scheme.");
            }
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
