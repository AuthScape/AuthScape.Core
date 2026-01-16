using AuthScape.CRM.Interfaces;
using AuthScape.CRM.Models;
using AuthScape.CRM.Models.Enums;
using AuthScape.CRM.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthScape.CRM.Services;

/// <summary>
/// Factory for creating CRM provider instances based on provider type
/// </summary>
public class CrmProviderFactory : ICrmProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<CrmProviderType, ICrmProvider> _providerCache = new();

    public CrmProviderFactory(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    public ICrmProvider GetProvider(CrmProviderType providerType)
    {
        if (_providerCache.TryGetValue(providerType, out var cached))
            return cached;

        var provider = CreateProvider(providerType);
        _providerCache[providerType] = provider;
        return provider;
    }

    public ICrmProvider GetProvider(CrmConnection connection)
    {
        return GetProvider(connection.Provider);
    }

    public IEnumerable<CrmProviderType> GetAvailableProviders()
    {
        return new[]
        {
            CrmProviderType.Dynamics365,
            CrmProviderType.HubSpot,
            CrmProviderType.GoogleContacts,
            CrmProviderType.MicrosoftGraph,
            CrmProviderType.SendGridContacts
        };
    }

    public bool IsProviderSupported(CrmProviderType providerType)
    {
        return providerType switch
        {
            CrmProviderType.Dynamics365 => true,
            CrmProviderType.HubSpot => false,      // TODO: Implement
            CrmProviderType.GoogleContacts => false, // TODO: Implement
            CrmProviderType.MicrosoftGraph => false, // TODO: Implement
            CrmProviderType.SendGridContacts => false, // TODO: Implement
            CrmProviderType.Salesforce => false,  // TODO: Implement
            _ => false
        };
    }

    private ICrmProvider CreateProvider(CrmProviderType providerType)
    {
        return providerType switch
        {
            CrmProviderType.Dynamics365 => CreateDynamicsProvider(),
            CrmProviderType.HubSpot => throw new NotImplementedException("HubSpot provider not yet implemented"),
            CrmProviderType.GoogleContacts => throw new NotImplementedException("Google Contacts provider not yet implemented"),
            CrmProviderType.MicrosoftGraph => throw new NotImplementedException("Microsoft Graph provider not yet implemented"),
            CrmProviderType.SendGridContacts => throw new NotImplementedException("SendGrid Contacts provider not yet implemented"),
            CrmProviderType.Salesforce => throw new NotImplementedException("Salesforce provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}")
        };
    }

    private DynamicsProvider CreateDynamicsProvider()
    {
        // Get Dynamics configuration from appsettings (check both paths for flexibility)
        var clientId = _configuration["AppSettings:CRM:Dynamics:ClientId"] ?? _configuration["CRM:Dynamics:ClientId"] ?? "";
        var clientSecret = _configuration["AppSettings:CRM:Dynamics:ClientSecret"] ?? _configuration["CRM:Dynamics:ClientSecret"] ?? "";
        var tenantId = _configuration["AppSettings:CRM:Dynamics:TenantId"] ?? _configuration["CRM:Dynamics:TenantId"] ?? "";

        var logger = _loggerFactory.CreateLogger<DynamicsProvider>();
        return new DynamicsProvider(clientId, clientSecret, tenantId, _httpClientFactory, logger);
    }
}
