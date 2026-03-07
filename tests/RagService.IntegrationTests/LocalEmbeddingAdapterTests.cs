using RagService.Infrastructure.Embeddings;

namespace RagService.IntegrationTests;

public sealed class LocalEmbeddingAdapterTests : IDisposable
{
    private readonly LocalEmbeddingAdapter _sut = new();

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsSuccessWith384Dimensions()
    {
        var result = await _sut.GenerateEmbeddingAsync("child welfare case involving neglect");

        Assert.True(result.IsSuccess);
        Assert.Equal(384, result.Value.Length);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SameTextProducesSameEmbedding()
    {
        var r1 = await _sut.GenerateEmbeddingAsync("patient presents with headache");
        var r2 = await _sut.GenerateEmbeddingAsync("patient presents with headache");

        Assert.Equal(r1.Value, r2.Value);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_DifferentTextProducesDifferentEmbedding()
    {
        var r1 = await _sut.GenerateEmbeddingAsync("child welfare abuse case");
        var r2 = await _sut.GenerateEmbeddingAsync("housing assistance for elderly");

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SemanticallySimilarTextsHaveHigherSimilarity()
    {
        var childAbuse = await _sut.GenerateEmbeddingAsync("child welfare case involving physical abuse");
        var childNeglect = await _sut.GenerateEmbeddingAsync("child welfare case involving neglect and maltreatment");
        var housing = await _sut.GenerateEmbeddingAsync("housing assistance application for homeless individual");

        var similarScore = CosineSimilarity(childAbuse.Value, childNeglect.Value);
        var dissimilarScore = CosineSimilarity(childAbuse.Value, housing.Value);

        Assert.True(similarScore > dissimilarScore,
            $"Similar texts should score higher ({similarScore:F4}) than dissimilar texts ({dissimilarScore:F4})");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ValuesAreL2Normalized()
    {
        var result = await _sut.GenerateEmbeddingAsync("test text for normalization check");

        Assert.True(result.IsSuccess);
        var norm = Math.Sqrt(result.Value.Sum(v => (double)v * v));
        Assert.InRange(norm, 0.99, 1.01);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyText_ReturnsFailure()
    {
        var result = await _sut.GenerateEmbeddingAsync("");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhitespaceText_ReturnsFailure()
    {
        var result = await _sut.GenerateEmbeddingAsync("   ");

        Assert.True(result.IsFailure);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
