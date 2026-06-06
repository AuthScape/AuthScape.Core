using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Validation.AspNetCore;
using Services.Context;

namespace AuthScape.AuthManager.OpenIddict;

/// <summary>
/// Builder extension that registers OpenIddict as the AuthScape identity provider.
/// </summary>
public static class OpenIddictRegistrationExtensions
{
    // Matches JwtBearerDefaults.AuthenticationScheme / the "Bearer" entry in
    // AuthScapeAuthorizeAttribute.SchemeList, without taking a dependency on the JwtBearer package.
    private const string JwtBearerSchemeName = "Bearer";


    /// <summary>
    /// Register the OpenIddict adapter. Wires the AuthScape-layer services (provider, normalizer,
    /// provisioning) plus the OpenIddict core (EF + DbContext) and, when
    /// <see cref="OpenIddictProviderOptions.UseIntrospection"/> is true, the validation handler
    /// configured for RFC 7662 introspection against the IDP.
    /// </summary>
    public static IAuthScapeIdentityBuilder UseOpenIddict(
        this IAuthScapeIdentityBuilder builder,
        Action<OpenIddictProviderOptions>? configure = null)
    {
        builder.EnsureNoActiveProvider("OpenIddict");

        // Probe the options once so we can branch validation wiring at registration time. The same
        // delegate is also registered against the options system so runtime callers see identical
        // values.
        var probed = new OpenIddictProviderOptions();
        configure?.Invoke(probed);

        if (configure != null)
            builder.Services.Configure(configure);
        else
            builder.Services.AddOptions<OpenIddictProviderOptions>();

        builder.Services.PostConfigure<AuthScapeIdentityOptions>(o => o.Mode = AuthProviderMode.Issuing);

        builder.Services.AddSingleton<IAuthProvider, OpenIddictIdentityProvider>();
        builder.Services.AddSingleton<IIdentityProvider, OpenIddictIdentityProvider>();
        builder.Services.AddSingleton<IClaimsNormalizer, OpenIddictClaimsNormalizer>();

        builder.Services.TryAddScoped<IUserProvisioningService, OpenIddictUserProvisioningService>();
        builder.Services.TryAddScoped<IAuthScapeSignupService, OpenIddictSignupService>();

        // OpenIddict core: lets the EF entities (applications, authorizations, scopes, tokens) be
        // discovered against the AuthScape DbContext. The host's IDP host adds .AddServer(...) on
        // top; the API host adds .AddValidation(...) below when UseIntrospection is true.
        var ob = builder.Services.AddOpenIddict()
            .AddCore(opts =>
            {
                opts.UseEntityFrameworkCore().UseDbContext<DatabaseContext>();
            });

        if (probed.UseIntrospection)
        {
            ValidateIntrospectionOptions(probed);

            ob.AddValidation(opts =>
            {
                opts.SetIssuer(probed.Issuer!);
                opts.AddAudiences(probed.Audience!);
                opts.UseIntrospection()
                    .SetClientId(probed.IntrospectionClientId!)
                    .SetClientSecret(probed.IntrospectionClientSecret!);
                opts.UseSystemNetHttp();
                opts.UseAspNetCore();
            });

            // [AuthScapeAuthorize] lists a "Bearer" scheme so the same attribute keeps working when the
            // host swaps OpenIddict for Keycloak (local JWT). In OpenIddict introspection mode there is
            // no JwtBearer handler, so register "Bearer" as a forwarding alias to the validation handler.
            // Without this, authorizing against the "Bearer" scheme throws
            // "No authentication handler is registered for the scheme 'Bearer'".
            builder.Services.AddAuthentication()
                .AddPolicyScheme(
                    JwtBearerSchemeName,
                    JwtBearerSchemeName,
                    o => o.ForwardDefault = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }

        builder.ActiveProviderId = "openiddict";
        return builder;
    }

    private static void ValidateIntrospectionOptions(OpenIddictProviderOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.Issuer))
        {
            throw new InvalidOperationException(
                "OpenIddictProviderOptions.Issuer is required when UseIntrospection is true.");
        }
        if (string.IsNullOrWhiteSpace(o.Audience))
        {
            throw new InvalidOperationException(
                "OpenIddictProviderOptions.Audience is required when UseIntrospection is true.");
        }
        if (string.IsNullOrWhiteSpace(o.IntrospectionClientId)
            || string.IsNullOrWhiteSpace(o.IntrospectionClientSecret))
        {
            throw new InvalidOperationException(
                "OpenIddictProviderOptions.IntrospectionClientId and IntrospectionClientSecret "
                + "are required when UseIntrospection is true.");
        }
    }
}
