using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.AuthManager;

/// <summary>
/// Fluent registration surface for the AuthScape identity pipeline. Returned from
/// <c>AddAuthScapeIdentity()</c>. Exactly one Use* call must be made — calling more than one
/// throws at startup because Issuing and Validating modes are mutually exclusive.
/// </summary>
public interface IAuthScapeIdentityBuilder
{
    /// <summary>Underlying service collection — adapter packages add their own services through this.</summary>
    IServiceCollection Services { get; }

    /// <summary>Tracks the active provider id once a Use* method has been called. Null until then.
    /// Used internally to detect duplicate provider registrations.</summary>
    string? ActiveProviderId { get; set; }
}

/// <summary>
/// Strongly-typed options used when registering a provider through
/// <see cref="AuthScapeIdentityBuilderExtensions.UseProvider{TProvider}"/>.
/// </summary>
public class ProviderRegistrationOptions
{
}
