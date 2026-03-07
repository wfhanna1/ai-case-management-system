namespace RagService.Infrastructure.Embeddings;

public sealed record EmbeddingSettings
{
    public string Provider { get; init; } = "local";
}
