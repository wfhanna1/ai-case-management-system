using SharedKernel;

namespace RagService.Domain.Ports;

/// <summary>
/// Port interface for generating vector embeddings from text.
/// Implementations adapt to specific embedding models (OpenAI, local models, etc.).
/// </summary>
public interface IEmbeddingPort
{
    Task<Result<float[]>> GenerateEmbeddingAsync(
        string text,
        CancellationToken ct = default);
}
