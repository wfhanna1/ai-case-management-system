using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Application;

/// <summary>
/// Finds documents similar to a given document by retrieving its stored embedding
/// from the vector store and running a similarity search.
/// </summary>
public sealed class SimilarDocumentsHandler
{
    private readonly IVectorStorePort _vectorStore;

    public SimilarDocumentsHandler(IVectorStorePort vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public async Task<Result<IReadOnlyList<SearchHit>>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        int topK = 5,
        CancellationToken ct = default)
    {
        // Retrieve the stored embedding for this document
        var embeddingResult = await _vectorStore.GetEmbeddingAsync(documentId, ct);

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
