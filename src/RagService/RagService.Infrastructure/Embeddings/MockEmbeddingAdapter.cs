using System.Security.Cryptography;
using System.Text;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Infrastructure.Embeddings;

/// <summary>
/// Deterministic mock embedding adapter that produces 384-dimensional unit vectors
/// from text via SHA-256 hashing. Same input always yields the same embedding.
/// Replace with OpenAIEmbeddingAdapter for production use.
/// </summary>
public sealed class MockEmbeddingAdapter : IEmbeddingPort
{
    private const int Dimensions = 384;

    public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var rng = new Random(BitConverter.ToInt32(hash, 0));

        var embedding = new float[Dimensions];
        double sumSq = 0;
        for (var i = 0; i < Dimensions; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2 - 1);
            sumSq += embedding[i] * embedding[i];
        }

        // L2 normalize
        var norm = (float)Math.Sqrt(sumSq);
        if (norm > 0)
        {
            for (var i = 0; i < Dimensions; i++)
                embedding[i] /= norm;
        }

        return Task.FromResult(Result<float[]>.Success(embedding));
    }
}
