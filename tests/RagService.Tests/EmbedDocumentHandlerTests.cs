using RagService.Application;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Tests;

public sealed class EmbedDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_EmbedAndStore_ReturnsSuccess()
    {
        var embedding = new StubEmbeddingPort(new float[384]);
        var store = new StubVectorStore();
        var sut = new EmbedDocumentHandler(embedding, store);

        var result = await sut.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "some text",
            new Dictionary<string, string> { ["Name"] = "Alice" });

        Assert.True(result.IsSuccess);
        Assert.Single(store.Upserted);
    }

    [Fact]
    public async Task HandleAsync_WhenEmbeddingFails_ReturnsFailure()
    {
        var embedding = new StubEmbeddingPort(null);
        var store = new StubVectorStore();
        var sut = new EmbedDocumentHandler(embedding, store);

        var result = await sut.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "text",
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Empty(store.Upserted);
    }

    [Fact]
    public async Task HandleAsync_WhenStoreFails_ReturnsFailure()
    {
        var embedding = new StubEmbeddingPort(new float[384]);
        var store = new StubVectorStore(failOnUpsert: true);
        var sut = new EmbedDocumentHandler(embedding, store);

        var result = await sut.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), "text",
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectDataToStore()
    {
        var embedding = new StubEmbeddingPort(new float[] { 1.0f, 2.0f, 3.0f });
        var store = new StubVectorStore();
        var sut = new EmbedDocumentHandler(embedding, store);

        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fields = new Dictionary<string, string> { ["Field1"] = "Value1" };

        await sut.HandleAsync(docId, tenantId, "text", fields);

        var (storedDocId, storedTenantId, storedEmbedding, storedMetadata) = store.Upserted[0];
        Assert.Equal(docId, storedDocId);
        Assert.Equal(tenantId, storedTenantId);
        Assert.Equal(new float[] { 1.0f, 2.0f, 3.0f }, storedEmbedding);
        Assert.Equal("Value1", storedMetadata["Field1"]);
    }

    // --- Test doubles ---

    private sealed class StubEmbeddingPort : IEmbeddingPort
    {
        private readonly float[]? _embedding;

        public StubEmbeddingPort(float[]? embedding) => _embedding = embedding;

        public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (_embedding is null)
                return Task.FromResult(Result<float[]>.Failure(new Error("EMBED_FAIL", "Embedding failed")));

            return Task.FromResult(Result<float[]>.Success(_embedding));
        }
    }

    private sealed class StubVectorStore : IVectorStorePort
    {
        private readonly bool _failOnUpsert;
        public List<(Guid DocId, Guid TenantId, float[] Embedding, Dictionary<string, string> Metadata)> Upserted { get; } = [];

        public StubVectorStore(bool failOnUpsert = false) => _failOnUpsert = failOnUpsert;

        public Task<Result<Unit>> UpsertAsync(
            Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
        {
            if (_failOnUpsert)
                return Task.FromResult(Result<Unit>.Failure(new Error("STORE_FAIL", "Store failed")));

            Upserted.Add((documentId, tenantId, embedding, metadata));
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
        {
            return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(
                Array.Empty<SearchHit>()));
        }
    }
}
