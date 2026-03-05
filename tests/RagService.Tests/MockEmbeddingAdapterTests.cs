using RagService.Infrastructure.Embeddings;

namespace RagService.Tests;

public sealed class MockEmbeddingAdapterTests
{
    private readonly MockEmbeddingAdapter _sut = new();

    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsSuccessWithCorrectDimension()
    {
        var result = await _sut.GenerateEmbeddingAsync("hello world");

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
        var r1 = await _sut.GenerateEmbeddingAsync("patient has flu");
        var r2 = await _sut.GenerateEmbeddingAsync("housing assistance needed");

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyText_ReturnsSuccess()
    {
        var result = await _sut.GenerateEmbeddingAsync("");

        Assert.True(result.IsSuccess);
        Assert.Equal(384, result.Value.Length);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ValuesAreNormalized()
    {
        var result = await _sut.GenerateEmbeddingAsync("test text for normalization");

        Assert.True(result.IsSuccess);
        // Check L2 norm is approximately 1.0
        var norm = Math.Sqrt(result.Value.Sum(v => (double)v * v));
        Assert.InRange(norm, 0.99, 1.01);
    }
}
