using AuthScape.AI.Enums;

namespace AuthScape.AI.Providers;

public class ProviderCapabilities
{
    public AICapability SupportedCapabilities { get; init; } = AICapability.Chat;

    public bool Supports(AICapability capability) =>
        (SupportedCapabilities & capability) == capability;
}
