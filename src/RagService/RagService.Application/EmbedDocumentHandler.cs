using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Application;

/// <summary>
/// Orchestrates embedding generation and vector storage for a processed document.
/// </summary>
public sealed class EmbedDocumentHandler
{
    private readonly IEmbeddingPort _embeddingPort;
    private readonly IVectorStorePort _vectorStore;

    public EmbedDocumentHandler(IEmbeddingPort embeddingPort, IVectorStorePort vectorStore)
    {
        _embeddingPort = embeddingPort;
        _vectorStore = vectorStore;
    }

    public async Task<Result<Unit>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        string textContent,
        Dictionary<string, string> fieldValues,
        CancellationToken ct = default)
    {
        var embeddingResult = await _embeddingPort.GenerateEmbeddingAsync(textContent, ct);
        if (embeddingResult.IsFailure)
            return Result<Unit>.Failure(embeddingResult.Error);

        return await _vectorStore.UpsertAsync(
            documentId, tenantId, embeddingResult.Value, fieldValues, ct);
    }
}
