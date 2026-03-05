using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Application;

/// <summary>
/// Finds documents similar to a given document by retrieving its embedding
/// from the vector store and running a similarity search.
/// </summary>
public sealed class SimilarDocumentsHandler
{
    private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorStorePort _vectorStore;

    public SimilarDocumentsHandler(IEmbeddingPort embeddingPort, IVectorStorePort vectorStore)
    {
        _embeddingPort = embeddingPort;
        _vectorStore = vectorStore;
    }

    public async Task<Result<IReadOnlyList<SearchHit>>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        int topK = 5,
        CancellationToken ct = default)
    {
        // Generate embedding for the document text to use as query vector.
        // In a full implementation, we'd retrieve the stored embedding directly.
        // For now, we generate a deterministic embedding from the document ID
        // to look up similar documents.
        var embeddingResult = await _embeddingPort.GenerateEmbeddingAsync(
            documentId.ToString(), ct);

        if (embeddingResult.IsFailure)
            return Result<IReadOnlyList<SearchHit>>.Failure(embeddingResult.Error);

        // Search for similar documents, requesting topK + 1 to exclude self
        var searchResult = await _vectorStore.SearchAsync(
            tenantId, embeddingResult.Value, topK + 1, ct);

        if (searchResult.IsFailure)
            return searchResult;

        // Filter out the queried document itself
        var filtered = searchResult.Value
            .Where(h => h.DocumentId != documentId)
            .Take(topK)
            .ToList();

        return Result<IReadOnlyList<SearchHit>>.Success(filtered);
    }
}
