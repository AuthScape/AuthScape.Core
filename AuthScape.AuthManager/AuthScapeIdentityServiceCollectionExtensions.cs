using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuthScape.AuthManager;

/// <summary>
/// Entry point for wiring AuthScape's provider-agnostic identity pipeline into the DI container.
/// </summary>
public static class AuthScapeIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core identity pipeline: options, normalizer registry, provisioning placeholder.
    /// Call exactly once. Follow with <c>.UseOpenIddict()</c> or <c>.UseKeycloak()</c> to attach a provider.
    /// </summary>
    public static IAuthScapeIdentityBuilder AddAuthScapeIdentity(
        this IServiceCollection services,
        Action<AuthScapeIdentityOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.AddOptions<AuthScapeIdentityOptions>();

        services.TryAddSingleton<ClaimsNormalizerRegistry>();

        return new AuthScapeIdentityBuilder(services);
    }
}

/// <summary>
/// Resolves the right <see cref="IClaimsNormalizer"/> for a given provider id. Resolved lazily
/// from DI so adapters can register their normalizers independently of registration order.
/// </summary>
public sealed class ClaimsNormalizerRegistry
{
    private readonly IServiceProvider services;

    public ClaimsNormalizerRegistry(IServiceProvider services)
    {
        this.services = services;
    }

    /// <summary>Find the normalizer that handles the given provider id. Throws when none registered.</summary>
    public IClaimsNormalizer For(string providerId)
    {
        var normalizers = services.GetServices<IClaimsNormalizer>();
        var match = normalizers.FirstOrDefault(n =>
            string.Equals(n.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            throw new InvalidOperationException(
                $"No IClaimsNormalizer registered for providerId '{providerId}'.");
        return match;
    }
}
