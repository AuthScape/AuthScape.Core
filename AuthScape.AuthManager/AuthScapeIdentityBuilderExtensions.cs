using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.AuthManager;

/// <summary>
/// Generic builder helpers shared by all provider adapters. Provider-specific extensions
/// (UseOpenIddict, UseKeycloak) live in their respective adapter packages.
/// </summary>
public static class AuthScapeIdentityBuilderExtensions
{
    /// <summary>
    /// Register a custom provider type that implements <see cref="IAuthProvider"/> (and typically
    /// also <see cref="IExternalTokenValidator"/> or <see cref="IIdentityProvider"/>).
    /// </summary>
    public static IAuthScapeIdentityBuilder UseProvider<TProvider>(
        this IAuthScapeIdentityBuilder builder,
        Action<AuthScapeIdentityOptions>? configure = null)
        where TProvider : class, IAuthProvider
    {
        builder.EnsureNoActiveProvider(typeof(TProvider).Name);

        if (configure != null)
            builder.Services.Configure(configure);

        builder.Services.AddSingleton<IAuthProvider, TProvider>();
        builder.ActiveProviderId = typeof(TProvider).Name;
        return builder;
    }

    /// <summary>
    /// Guards against registering two providers in the same pipeline. Issuing and Validating modes
    /// are mutually exclusive, so two Use* calls in the same DI container is a configuration error.
    /// </summary>
    public static void EnsureNoActiveProvider(this IAuthScapeIdentityBuilder builder, string newProviderName)
    {
        if (!string.IsNullOrEmpty(builder.ActiveProviderId))
        {
            throw new InvalidOperationException(
                $"AuthScape identity provider '{builder.ActiveProviderId}' is already registered. " +
                $"Cannot also register '{newProviderName}' — Issuing and Validating modes are mutually exclusive. " +
                $"Pick one provider per host.");
        }
    }
}
