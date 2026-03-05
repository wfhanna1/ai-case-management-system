using RagService.Application;
using RagService.Domain.Ports;
using SharedKernel;

namespace RagService.Tests;

public sealed class SimilarDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsSimilarDocuments_ExcludingSelf()
    {
        var docId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var store = new StubVectorStore(
        [
            new SearchHit(docId, 1.0, new Dictionary<string, string>()),
            new SearchHit(otherId, 0.9, new Dictionary<string, string> { ["Name"] = "Alice" }),
        ]);
        var sut = new SimilarDocumentsHandler(new StubEmbeddingPort(), store);

        var result = await sut.HandleAsync(docId, tenantId, 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(otherId, result.Value[0].DocumentId);
    }

    [Fact]
    public async Task HandleAsync_WhenEmbeddingFails_ReturnsFailure()
    {
        var store = new StubVectorStore([]);
        var sut = new SimilarDocumentsHandler(new StubEmbeddingPort(fail: true), store);

        var result = await sut.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_WhenSearchFails_ReturnsFailure()
    {
        var store = new StubVectorStore([], failOnSearch: true);
        var sut = new SimilarDocumentsHandler(new StubEmbeddingPort(), store);

        var result = await sut.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), 5);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task HandleAsync_RespectsTopK()
    {
        var docId = Guid.NewGuid();
        var hits = Enumerable.Range(0, 10)
            .Select(i => new SearchHit(Guid.NewGuid(), 0.9 - i * 0.01, new Dictionary<string, string>()))
            .ToList();

        var store = new StubVectorStore(hits);
        var sut = new SimilarDocumentsHandler(new StubEmbeddingPort(), store);

        var result = await sut.HandleAsync(docId, Guid.NewGuid(), 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
    }

    // --- Test doubles ---

    private sealed class StubEmbeddingPort : IEmbeddingPort
    {
        private readonly bool _fail;
        public StubEmbeddingPort(bool fail = false) => _fail = fail;

        public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (_fail)
                return Task.FromResult(Result<float[]>.Failure(new Error("FAIL", "Failed")));
            return Task.FromResult(Result<float[]>.Success(new float[384]));
        }
    }

    private sealed class StubVectorStore : IVectorStorePort
    {
        private readonly IReadOnlyList<SearchHit> _hits;
        private readonly bool _failOnSearch;

        public StubVectorStore(IReadOnlyList<SearchHit> hits, bool failOnSearch = false)
        {
            _hits = hits;
            _failOnSearch = failOnSearch;
        }

        public Task<Result<Unit>> UpsertAsync(
            Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
        {
            if (_failOnSearch)
                return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Failure(
                    new Error("FAIL", "Search failed")));
            return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(_hits));
        }
    }
}
