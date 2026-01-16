using AuthScape.CRM.Models;
using AuthScape.CRM.Models.Enums;

namespace AuthScape.CRM.Interfaces;

/// <summary>
/// Factory for creating CRM provider instances based on provider type
/// </summary>
public interface ICrmProviderFactory
{
    /// <summary>
    /// Gets a CRM provider instance for the specified provider type
    /// </summary>
    ICrmProvider GetProvider(CrmProviderType providerType);

    /// <summary>
    /// Gets a CRM provider instance for the specified connection
    /// </summary>
    ICrmProvider GetProvider(CrmConnection connection);

    /// <summary>
    /// Gets all available provider types
    /// </summary>
    IEnumerable<CrmProviderType> GetAvailableProviders();

    /// <summary>
    /// Checks if a provider type is supported
    /// </summary>
    bool IsProviderSupported(CrmProviderType providerType);
}
