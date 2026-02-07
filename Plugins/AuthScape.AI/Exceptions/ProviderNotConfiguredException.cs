using AuthScape.AI.Enums;

namespace AuthScape.AI.Exceptions;

public class ProviderNotConfiguredException : AIServiceException
{
    public AIProvider Provider { get; }

    public ProviderNotConfiguredException(AIProvider provider)
        : base($"AI provider '{provider}' is not configured. Ensure it is registered during service configuration.")
    {
        Provider = provider;
    }
}
