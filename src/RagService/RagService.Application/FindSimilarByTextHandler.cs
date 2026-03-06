using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Application;

/// <summary>
/// Finds similar documents by generating an embedding from the provided text content
/// on-the-fly and searching the vector store. This avoids requiring the queried document
/// to have a pre-stored embedding.
/// </summary>
public sealed class FindSimilarByTextHandler
{
    private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorStorePort _vectorStore;

    public FindSimilarByTextHandler(IEmbeddingPort embeddingPort, IVectorStorePort vectorStore)
    {
        _embeddingPort = embeddingPort;
        _vectorStore = vectorStore;
    }

    public async Task<Result<IReadOnlyList<SearchHit>>> HandleAsync(
        string textContent,
        Guid tenantId,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(textContent))
            return Result<IReadOnlyList<SearchHit>>.Failure(
                new Error("EMPTY_TEXT", "Text content is required for similarity search"));

        var embeddingResult = await _embeddingPort.GenerateEmbeddingAsync(textContent, ct);
        if (embeddingResult.IsFailure)
            return Result<IReadOnlyList<SearchHit>>.Failure(embeddingResult.Error);

        return await _vectorStore.SearchAsync(tenantId, embeddingResult.Value, topK, ct);
    }
}
