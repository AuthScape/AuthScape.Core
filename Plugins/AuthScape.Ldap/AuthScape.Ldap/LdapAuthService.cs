using AuthScape.Ldap.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using Services.Context;
using System.Text.Json;

namespace AuthScape.Ldap;

public class LdapAuthService : ILdapAuthService
{
    private const string DataProtectionPurpose = "AuthScape.Ldap.ServiceAccountPassword";

    private readonly DatabaseContext db;
    private readonly IDataProtectionProvider dataProtection;
    private readonly ILogger<LdapAuthService> logger;

    public LdapAuthService(
        DatabaseContext db,
        IDataProtectionProvider dataProtection,
        ILogger<LdapAuthService> logger)
    {
        this.db = db;
        this.dataProtection = dataProtection;
        this.logger = logger;
    }

    public async Task<LdapConfiguration?> ResolveConfigAsync(
        string emailOrUsername,
        long? companyId,
        long? explicitConfigId,
        CancellationToken cancellationToken = default)
    {
        if (explicitConfigId.HasValue)
        {
            return await db.Set<LdapConfiguration>()
                .FirstOrDefaultAsync(c => c.Id == explicitConfigId.Value && c.IsEnabled, cancellationToken);
        }

        var configs = await db.Set<LdapConfiguration>()
            .Where(c => c.IsEnabled)
            .Where(c => c.CompanyId == companyId || c.CompanyId == null)
            .ToListAsync(cancellationToken);

        // Prefer tenant-specific over global.
        configs = configs.OrderBy(c => c.CompanyId == null ? 1 : 0).ToList();

        // Match by EmailDomainHint when input looks like an email.
        if (!string.IsNullOrEmpty(emailOrUsername) && emailOrUsername.Contains('@'))
        {
            var domain = emailOrUsername.Split('@', 2)[1];
            var byDomain = configs.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.EmailDomainHint)
                && c.EmailDomainHint.Split(',', StringSplitOptions.TrimEntries)
                    .Any(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase)));
            if (byDomain != null) return byDomain;
        }

        // Otherwise the first enabled config wins (tenant-specific preferred above).
        return configs.FirstOrDefault();
    }

    public async Task<LdapAuthResult> AuthenticateAsync(
        string username,
        string password,
        long configId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return LdapAuthResult.Failed("username and password required");

        var config = await db.Set<LdapConfiguration>()
            .FirstOrDefaultAsync(c => c.Id == configId && c.IsEnabled, cancellationToken);
        if (config == null)
            return LdapAuthResult.Failed("Configuration not found or disabled");

        try
        {
            using var conn = new LdapConnection { SecureSocketLayer = config.ServerUrl.StartsWith("ldaps://", StringComparison.OrdinalIgnoreCase) };
            var (host, port) = ParseServerUrl(config.ServerUrl);
            await Task.Run(() => conn.Connect(host, port), cancellationToken);

            if (config.UseStartTls && !conn.SecureSocketLayer)
            {
                await Task.Run(conn.StartTls, cancellationToken);
            }

            // If a user filter + service account is configured, do a pre-bind search to find the actual DN.
            string bindDn;
            if (!string.IsNullOrEmpty(config.UserFilter)
                && !string.IsNullOrEmpty(config.SearchBase)
                && !string.IsNullOrEmpty(config.ServiceAccountDn))
            {
                bindDn = await SearchForUserDnAsync(conn, config, username, cancellationToken)
                    ?? RenderBindDn(config.BindDnTemplate, username);
            }
            else
            {
                bindDn = RenderBindDn(config.BindDnTemplate, username);
            }

            try
            {
                await Task.Run(() => conn.Bind(bindDn, password), cancellationToken);
            }
            catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
            {
                return LdapAuthResult.Failed("Invalid credentials");
            }

            // Fetch attributes for claim mapping. Best-effort; don't fail auth if attribute read fails.
            var attributes = new Dictionary<string, string>();
            try
            {
                if (!string.IsNullOrEmpty(config.AttributeMappingsJson))
                {
                    var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(config.AttributeMappingsJson)
                                   ?? new Dictionary<string, string>();
                    var attributeNames = mappings.Values.Distinct().ToArray();
                    if (attributeNames.Length > 0)
                    {
                        var entry = await Task.Run(() => conn.Read(bindDn, attributeNames), cancellationToken);
                        foreach (var (claim, ldapAttr) in mappings)
                        {
                            var attr = entry?.GetAttribute(ldapAttr);
                            if (attr?.StringValue is { Length: > 0 } v)
                                attributes[claim] = v;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LDAP attribute fetch failed for DN {Dn} (auth still succeeded)", bindDn);
            }

            return LdapAuthResult.Ok(bindDn, attributes);
        }
        catch (LdapException ex)
        {
            logger.LogWarning(ex, "LDAP error during authentication: code={Code}", ex.ResultCode);
            return LdapAuthResult.Failed("LDAP server error");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during LDAP authentication");
            return LdapAuthResult.Failed("LDAP connection failed");
        }
    }

    private async Task<string?> SearchForUserDnAsync(
        LdapConnection conn,
        LdapConfiguration config,
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            var serviceAccountPassword = string.IsNullOrEmpty(config.ServiceAccountPasswordEncrypted)
                ? null
                : dataProtection.CreateProtector(DataProtectionPurpose).Unprotect(config.ServiceAccountPasswordEncrypted);

            await Task.Run(() => conn.Bind(config.ServiceAccountDn, serviceAccountPassword), cancellationToken);

            var filter = config.UserFilter!.Replace("{username}", LdapEscape(username));
            var results = await Task.Run(() => conn.Search(
                config.SearchBase, LdapConnection.ScopeSub, filter, new[] { "dn" }, false), cancellationToken);

            if (results.HasMore())
            {
                var entry = results.Next();
                return entry.Dn;
            }
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pre-bind search failed; falling back to BindDnTemplate");
            return null;
        }
    }

    private static string RenderBindDn(string template, string username) =>
        template.Replace("{username}", username);

    private static (string Host, int Port) ParseServerUrl(string url)
    {
        var u = new Uri(url);
        var port = u.Port > 0 ? u.Port : (u.Scheme == "ldaps" ? 636 : 389);
        return (u.Host, port);
    }

    /// <summary>Escape characters per RFC 4515 for LDAP filters.</summary>
    private static string LdapEscape(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
