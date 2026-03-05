using SharedKernel;

namespace RagService.Domain.Ports;

/// <summary>
/// Port interface for storing and querying vector embeddings.
/// Implementations adapt to vector databases (Qdrant, Pinecone, etc.).
/// </summary>
public interface IVectorStorePort
{
    Task<Result<Unit>> UpsertAsync(
        Guid documentId,
        Guid tenantId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
        Guid tenantId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken ct = default);

    Task<Result<float[]>> GetEmbeddingAsync(
        Guid documentId,
        CancellationToken ct = default);
}

public sealed record SearchHit(
    Guid DocumentId,
    double Score,
    Dictionary<string, string> Metadata);
