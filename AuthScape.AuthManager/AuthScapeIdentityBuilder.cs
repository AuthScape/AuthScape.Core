using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.AuthManager;

/// <summary>
/// Default <see cref="IAuthScapeIdentityBuilder"/> returned from <c>AddAuthScapeIdentity</c>.
/// </summary>
internal sealed class AuthScapeIdentityBuilder : IAuthScapeIdentityBuilder
{
    public AuthScapeIdentityBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    public string? ActiveProviderId { get; set; }
}
