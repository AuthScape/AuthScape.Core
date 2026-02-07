namespace AuthScape.AI.Enums;

[Flags]
public enum AICapability
{
    None = 0,
    Chat = 1 << 0,
    Streaming = 1 << 1,
    Embeddings = 1 << 2,
    Vision = 1 << 3,
    ToolCalling = 1 << 4,
    ModelManagement = 1 << 5,
    Audio = 1 << 6,
    ImageGeneration = 1 << 7,
    Memory = 1 << 8
}
