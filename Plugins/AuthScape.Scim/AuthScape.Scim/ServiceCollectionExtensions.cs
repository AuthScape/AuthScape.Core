using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.Scim;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IScimService"/> and the "ScimAccess" authorization policy.
    /// The policy requires a valid bearer token with a `scim_company_id` claim — issued
    /// by AuthScape's OpenIddict server when a per-tenant SCIM client authenticates via client_credentials.
    /// </summary>
    public static IServiceCollection AddAuthScapeScim(this IServiceCollection services)
    {
        services.AddScoped<IScimService, ScimService>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ScimAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scim_company_id");
            });
        });
        return services;
    }
}
