using AuthScape.AI.Enums;

namespace AuthScape.AI.Exceptions;

public class CapabilityNotSupportedException : AIServiceException
{
    public AIProvider Provider { get; }
    public AICapability Capability { get; }

    public CapabilityNotSupportedException(AIProvider provider, AICapability capability)
        : base($"Provider '{provider}' does not support the '{capability}' capability.")
    {
        Provider = provider;
        Capability = capability;
    }
}
