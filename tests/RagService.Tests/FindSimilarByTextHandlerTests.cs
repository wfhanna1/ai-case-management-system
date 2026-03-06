using RagService.Application;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Tests;

public sealed class FindSimilarByTextHandlerTests
{
    [Fact]
    public async Task HandleAsync_GeneratesEmbeddingAndSearches()
    {
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var embedding = new StubEmbeddingPort(new float[] { 0.1f, 0.2f, 0.3f });
        var store = new StubVectorStore(
            searchHits:
            [
                new SearchHit(otherId, 0.9, new Dictionary<string, string> { ["Name"] = "Alice" }),
            ]);
        var sut = new FindSimilarByTextHandler(embedding, store);

        var result = await sut.HandleAsync("Child welfare case for Alice", tenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(otherId, result.Value[0].DocumentId);
        Assert.Equal(new float[] { 0.1f, 0.2f, 0.3f }, store.LastQueryEmbedding);
    }

    [Fact]
    public async Task HandleAsync_WhenEmbeddingFails_ReturnsFailure()
    {
        var embedding = new StubEmbeddingPort(null);
        var store = new StubVectorStore(searchHits: []);
        var sut = new FindSimilarByTextHandler(embedding, store);

        var result = await sut.HandleAsync("some text", Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
        Assert.Equal("EMBEDDING_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenSearchFails_ReturnsFailure()
    {
        var embedding = new StubEmbeddingPort(new float[] { 0.1f });
        var store = new StubVectorStore(searchHits: [], failOnSearch: true);
        var sut = new FindSimilarByTextHandler(embedding, store);

        var result = await sut.HandleAsync("some text", Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_EmptyText_ReturnsFailure()
    {
        var embedding = new StubEmbeddingPort(new float[] { 0.1f });
        var store = new StubVectorStore(searchHits: []);
        var sut = new FindSimilarByTextHandler(embedding, store);

        var result = await sut.HandleAsync("", Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
        Assert.Equal("EMPTY_TEXT", result.Error.Code);
    }

    // --- Test doubles ---

    private sealed class StubEmbeddingPort : IEmbeddingPort
    {
        private readonly float[]? _result;
        public StubEmbeddingPort(float[]? result) => _result = result;

        public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (_result is null)
                return Task.FromResult(Result<float[]>.Failure(
                    new Error("EMBEDDING_FAILED", "Embedding generation failed")));
            return Task.FromResult(Result<float[]>.Success(_result));
        }
    }

    private sealed class StubVectorStore : IVectorStorePort
    {
        private readonly IReadOnlyList<SearchHit> _searchHits;
        private readonly bool _failOnSearch;

        public float[]? LastQueryEmbedding { get; private set; }

        public StubVectorStore(IReadOnlyList<SearchHit> searchHits, bool failOnSearch = false)
        {
            _searchHits = searchHits;
            _failOnSearch = failOnSearch;
        }

        public Task<Result<Unit>> UpsertAsync(
            Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
        {
            LastQueryEmbedding = queryEmbedding;
            if (_failOnSearch)
                return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Failure(
                    new Error("FAIL", "Search failed")));
            return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(_searchHits));
        }

        public Task<Result<float[]>> GetEmbeddingAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(Result<float[]>.Failure(
                new Error("NOT_USED", "Should not be called")));
    }
}
