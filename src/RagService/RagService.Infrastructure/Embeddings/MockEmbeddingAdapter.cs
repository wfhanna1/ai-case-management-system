using System.Security.Cryptography;
using System.Text;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Infrastructure.Embeddings;

/// <summary>
/// Deterministic mock embedding adapter that produces 384-dimensional unit vectors.
/// Documents sharing the same subject name cluster together: the first half of the
/// vector is derived from the subject (second token in the text, which is the name),
/// and the second half from the full text. This means follow-up documents for the
/// same person will have high cosine similarity, making the similar-cases feature
/// produce realistic results even with mock embeddings.
/// Replace with OpenAIEmbeddingAdapter for production use.
/// </summary>
public sealed class MockEmbeddingAdapter : IEmbeddingPort
{
    private const int Dimensions = 384;
    private const int IdentityDims = 256;
    private const int DetailDims = Dimensions - IdentityDims;

    public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var identity = ExtractIdentity(text);
        var identityHash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var detailHash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        var identityRng = new Random(BitConverter.ToInt32(identityHash, 0));
        var detailRng = new Random(BitConverter.ToInt32(detailHash, 0));

        var embedding = new float[Dimensions];
        double sumSq = 0;

        for (var i = 0; i < IdentityDims; i++)
        {
            embedding[i] = (float)(identityRng.NextDouble() * 2 - 1);
            sumSq += embedding[i] * embedding[i];
        }

        for (var i = IdentityDims; i < Dimensions; i++)
        {
            embedding[i] = (float)(detailRng.NextDouble() * 2 - 1) * 0.3f;
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

    /// <summary>
    /// Extracts the identity portion of the seeder text format:
    /// "TemplateType. SubjectName. Field: Value. ..."
    /// Returns "TemplateType.SubjectName" so same-person docs cluster by type and name.
    /// Falls back to the full text for non-seeder input.
    /// </summary>
    private static string ExtractIdentity(string text)
    {
        var parts = text.Split(". ", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0]}.{parts[1]}";
        return text;
    }
}
