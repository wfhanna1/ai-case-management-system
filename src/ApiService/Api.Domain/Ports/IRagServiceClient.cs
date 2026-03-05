using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Port interface for calling the RAG microservice to find similar documents.
/// </summary>
public interface IRagServiceClient
{
    Task<Result<IReadOnlyList<SimilarDocumentResult>>> FindSimilarAsync(
        Guid documentId,
        Guid tenantId,
        int topK = 5,
        CancellationToken ct = default);
}

public sealed record SimilarDocumentResult(
    Guid DocumentId,
    double Score,
    Dictionary<string, string> Metadata);
