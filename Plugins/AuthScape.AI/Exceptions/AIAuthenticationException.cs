using AuthScape.AI.Enums;

namespace AuthScape.AI.Exceptions;

public class AIAuthenticationException : AIServiceException
{
    public AIProvider Provider { get; }

    public AIAuthenticationException(AIProvider provider, Exception innerException)
        : base($"Authentication failed for provider '{provider}'. Check your API key.", innerException)
    {
        Provider = provider;
    }
}
