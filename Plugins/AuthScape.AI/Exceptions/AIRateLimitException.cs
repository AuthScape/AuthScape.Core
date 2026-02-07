using AuthScape.AI.Enums;

namespace AuthScape.AI.Exceptions;

public class AIRateLimitException : AIServiceException
{
    public AIProvider Provider { get; }

    public AIRateLimitException(AIProvider provider, Exception innerException)
        : base($"Rate limit exceeded for provider '{provider}'.", innerException)
    {
        Provider = provider;
    }
}
