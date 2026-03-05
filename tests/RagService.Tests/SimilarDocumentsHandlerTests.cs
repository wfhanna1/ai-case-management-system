using RagService.Application;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Tests;

public sealed class SimilarDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_RetrievesStoredEmbedding_AndSearchesSimilar()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var storedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        var store = new StubVectorStore(
            storedEmbeddings: new Dictionary<Guid, float[]> { [docId] = storedEmbedding },
            searchHits:
            [
                new SearchHit(docId, 1.0, new Dictionary<string, string>()),
                new SearchHit(otherId, 0.9, new Dictionary<string, string> { ["Name"] = "Alice" }),
            ]);
        var sut = new SimilarDocumentsHandler(store);

        var result = await sut.HandleAsync(docId, tenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(otherId, result.Value[0].DocumentId);
        // Verify the stored embedding was used for the search (not a UUID-derived one)
        Assert.Equal(storedEmbedding, store.LastQueryEmbedding);
    }

    [Fact]
    public async Task HandleAsync_WhenEmbeddingNotFound_ReturnsFailure()
    {
        var store = new StubVectorStore(
            storedEmbeddings: new Dictionary<Guid, float[]>(),
            searchHits: []);
        var sut = new SimilarDocumentsHandler(store);

        var result = await sut.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
        Assert.Equal("EMBEDDING_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenGetEmbeddingFails_ReturnsFailure()
    {
        var store = new StubVectorStore(
            storedEmbeddings: new Dictionary<Guid, float[]>(),
            searchHits: [],
            failOnGetEmbedding: true);
        var sut = new SimilarDocumentsHandler(store);

        var result = await sut.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_WhenSearchFails_ReturnsFailure()
    {
        var docId = Guid.NewGuid();
        var store = new StubVectorStore(
            storedEmbeddings: new Dictionary<Guid, float[]> { [docId] = new float[384] },
            searchHits: [],
            failOnSearch: true);
        var sut = new SimilarDocumentsHandler(store);

        var result = await sut.HandleAsync(docId, Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_RespectsTopK()
    {
        var docId = Guid.NewGuid();
        var hits = Enumerable.Range(0, 10)
            .Select(i => new SearchHit(Guid.NewGuid(), 0.9 - i * 0.01, new Dictionary<string, string>()))
            .ToList();

        var store = new StubVectorStore(
            storedEmbeddings: new Dictionary<Guid, float[]> { [docId] = new float[384] },
            searchHits: hits);
        var sut = new SimilarDocumentsHandler(store);

        var result = await sut.HandleAsync(docId, Guid.NewGuid(), 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task HandleAsync_ExcludesSelfFromResults()
    {
        var docId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var store = new StubVectorStore(
            storedEmbeddings: new Dictionary<Guid, float[]> { [docId] = new float[384] },
            searchHits:
            [
                new SearchHit(docId, 1.0, new Dictionary<string, string>()),
                new SearchHit(otherId, 0.85, new Dictionary<string, string>()),
            ]);
        var sut = new SimilarDocumentsHandler(store);

        var result = await sut.HandleAsync(docId, Guid.NewGuid(), 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(otherId, result.Value[0].DocumentId);
    }

    // --- Test doubles ---

    private sealed class StubVectorStore : IVectorStorePort
    {
        private readonly Dictionary<Guid, float[]> _storedEmbeddings;
        private readonly IReadOnlyList<SearchHit> _searchHits;
        private readonly bool _failOnSearch;
        private readonly bool _failOnGetEmbedding;

        public float[]? LastQueryEmbedding { get; private set; }

        public StubVectorStore(
            Dictionary<Guid, float[]> storedEmbeddings,
            IReadOnlyList<SearchHit> searchHits,
            bool failOnSearch = false,
            bool failOnGetEmbedding = false)
        {
            _storedEmbeddings = storedEmbeddings;
            _searchHits = searchHits;
            _failOnSearch = failOnSearch;
            _failOnGetEmbedding = failOnGetEmbedding;
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
        {
            if (_failOnGetEmbedding)
                return Task.FromResult(Result<float[]>.Failure(
                    new Error("FAIL", "Get embedding failed")));
            if (_storedEmbeddings.TryGetValue(documentId, out var embedding))
                return Task.FromResult(Result<float[]>.Success(embedding));
            return Task.FromResult(Result<float[]>.Failure(
                new Error("EMBEDDING_NOT_FOUND", $"No embedding found for document {documentId}")));
        }
    }
}
