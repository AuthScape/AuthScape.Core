using AuthScape.AI.Enums;

namespace AuthScape.AI.Exceptions;

public class ModelNotFoundException : AIServiceException
{
    public string ModelId { get; }
    public AIProvider Provider { get; }

    public ModelNotFoundException(string modelId, AIProvider provider)
        : base($"Model '{modelId}' was not found on provider '{provider}'.")
    {
        ModelId = modelId;
        Provider = provider;
    }
}
