using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// Builder extension that registers Keycloak as the AuthScape identity provider in
/// <see cref="AuthProviderMode.Validating"/> mode.
/// </summary>
public static class KeycloakRegistrationExtensions
{
    /// <summary>
    /// Register the Keycloak adapter. By default tokens are validated locally via JwtBearer + JWKS.
    /// Set <see cref="KeycloakProviderOptions.UseIntrospection"/> to true to route validation through
    /// Keycloak's RFC 7662 introspection endpoint instead — same pattern AuthScape uses for OpenIddict.
    /// </summary>
    public static IAuthScapeIdentityBuilder UseKeycloak(
        this IAuthScapeIdentityBuilder builder,
        Action<KeycloakProviderOptions>? configure = null)
    {
        builder.EnsureNoActiveProvider("Keycloak");

        // Probe the configured options once so we can branch between local JWT validation and
        // introspection at registration time. The same delegate is registered against the options
        // monitor below so runtime callers see the same values.
        var probedOptions = new KeycloakProviderOptions();
        configure?.Invoke(probedOptions);

        if (configure != null)
            builder.Services.Configure(configure);
        else
            builder.Services.AddOptions<KeycloakProviderOptions>();

        builder.Services.PostConfigure<AuthScapeIdentityOptions>(o =>
        {
            o.Mode = AuthProviderMode.Validating;
            o.DefaultProviderId = "keycloak";
        });

        builder.Services.AddSingleton<IAuthProvider, KeycloakTokenValidator>();
        builder.Services.AddSingleton<IExternalTokenValidator, KeycloakTokenValidator>();
        builder.Services.AddSingleton<IClaimsNormalizer, KeycloakClaimsNormalizer>();
        builder.Services.AddScoped<IUserProvisioningService, KeycloakUserProvisioningService>();
        builder.Services.AddScoped<IAuthScapeSignupService, KeycloakSignupService>();

        // ASP.NET claims transformation runs after token validation — this is where AuthScape
        // enriches HttpContext.User with mapped roles and the AuthScapeUserId claim. It is
        // pipeline-agnostic, so it works for both the JwtBearer path and the introspection path.
        builder.Services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();

        if (probedOptions.UseIntrospection)
        {
            WireIntrospection(builder.Services);
        }
        else
        {
            WireLocalJwt(builder.Services);
        }

        builder.ActiveProviderId = "keycloak";
        return builder;
    }

    /// <summary>
    /// Local JWT validation: JwtBearer middleware downloads JWKS from the realm's OIDC discovery
    /// document and validates signatures/expiry/issuer/audience in-process. No round-trip per request.
    /// </summary>
    private static void WireLocalJwt(IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<KeycloakProviderOptions>>((jwt, kcOpts) =>
            {
                var kc = kcOpts.Value;
                jwt.Authority = kc.Authority;
                jwt.Audience = kc.ClientId;
                jwt.RequireHttpsMetadata = kc.RequireHttpsMetadata;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = kc.Authority?.TrimEnd('/'),
                    ValidateAudience = !string.IsNullOrEmpty(kc.ClientId),
                    ValidAudience = kc.ClientId,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = kc.NameClaimName,
                    RoleClaimType = "roles",
                };
            });
    }

    /// <summary>
    /// RFC 7662 introspection via Keycloak's introspect endpoint. OpenIddict.Validation discovers the
    /// endpoint from the realm's /.well-known/openid-configuration document and calls it on every
    /// protected request, presenting <see cref="KeycloakProviderOptions.IntrospectionClientId"/> +
    /// <see cref="KeycloakProviderOptions.IntrospectionClientSecret"/> as basic-auth credentials.
    /// Matches the same OpenIddict introspection wiring AuthScape uses against its own IDP.
    /// </summary>
    private static void WireIntrospection(IServiceCollection services)
    {
        services
            .AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.UseIntrospection();
                options.UseSystemNetHttp();
                options.UseAspNetCore();
            });

        // Bind issuer/audience/client credentials from KeycloakProviderOptions after configuration
        // is bound, so the same options surface that drives the JwtBearer path also drives the
        // introspection client.
        services.AddOptions<OpenIddictValidationOptions>()
            .Configure<Microsoft.Extensions.Options.IOptions<KeycloakProviderOptions>>((validation, kcOpts) =>
            {
                var kc = kcOpts.Value;

                if (string.IsNullOrWhiteSpace(kc.Authority))
                {
                    throw new InvalidOperationException(
                        "KeycloakProviderOptions.Authority is required when UseIntrospection is true.");
                }

                if (string.IsNullOrWhiteSpace(kc.IntrospectionClientId)
                    || string.IsNullOrWhiteSpace(kc.IntrospectionClientSecret))
                {
                    throw new InvalidOperationException(
                        "KeycloakProviderOptions.IntrospectionClientId and IntrospectionClientSecret "
                        + "are required when UseIntrospection is true.");
                }

                var issuer = kc.Authority.EndsWith('/') ? kc.Authority : kc.Authority + "/";
                validation.Issuer = new Uri(issuer);

                if (!string.IsNullOrWhiteSpace(kc.ClientId))
                {
                    validation.Audiences.Add(kc.ClientId);
                }

                validation.ClientId = kc.IntrospectionClientId;
                validation.ClientSecret = kc.IntrospectionClientSecret;
            });
    }
}
