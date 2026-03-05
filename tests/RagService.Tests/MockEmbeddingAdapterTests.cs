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

    [Fact]
    public async Task GenerateEmbeddingAsync_SameSubjectClusters()
    {
        // Seeder text format: "TemplateType. SubjectName. Field: Value. ..."
        var r1 = await _sut.GenerateEmbeddingAsync("ChildWelfare. Emma Thompson. ChildName: Emma Thompson. Age: 5. ReasonForReferral: Physical abuse suspected");
        var r2 = await _sut.GenerateEmbeddingAsync("ChildWelfare. Emma Thompson. ChildName: Emma Thompson. ReportType: Follow-up Assessment");
        var r3 = await _sut.GenerateEmbeddingAsync("ChildWelfare. Liam Johnson. ChildName: Liam Johnson. Age: 8. ReasonForReferral: Neglect");

        static double Cosine(float[] a, float[] b) =>
            a.Zip(b).Sum(p => (double)p.First * p.Second);

        var samePerson = Cosine(r1.Value, r2.Value);
        var diffPerson = Cosine(r1.Value, r3.Value);

        // Same person should have higher similarity than different person
        Assert.True(samePerson > diffPerson,
            $"Same person cosine ({samePerson:F4}) should be > different person ({diffPerson:F4})");
        Assert.True(samePerson > 0.8, $"Same person cosine ({samePerson:F4}) should be > 0.8");
    }
}
