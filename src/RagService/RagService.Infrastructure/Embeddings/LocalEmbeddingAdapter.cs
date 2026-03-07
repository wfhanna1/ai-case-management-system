using RagService.Domain.Ports;
using SharedKernel;
using SmartComponents.LocalEmbeddings;

namespace RagService.Infrastructure.Embeddings;

public sealed class LocalEmbeddingAdapter : IEmbeddingPort, IDisposable
{
    private readonly LocalEmbedder _embedder = new();

    public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(Result<float[]>.Failure(
                new Error("EMBED_EMPTY", "Cannot generate embedding for empty or whitespace text.")));

        var embedding = _embedder.Embed(text);
        var values = embedding.Values.ToArray();

        // L2 normalize to unit length for cosine similarity in Qdrant
        var norm = (float)Math.Sqrt(values.Sum(v => (double)v * v));
        if (norm > 0)
        {
            for (var i = 0; i < values.Length; i++)
                values[i] /= norm;
        }

        return Task.FromResult(Result<float[]>.Success(values));
    }

    public void Dispose() => _embedder.Dispose();
}
