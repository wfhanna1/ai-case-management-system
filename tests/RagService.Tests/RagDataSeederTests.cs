using RagService.Domain.Ports;
using RagService.Host;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace RagService.Tests;

public sealed class RagDataSeederTests
{
    [Fact]
    public async Task StartAsync_SeedsOver200Embeddings()
    {
        var store = new CountingVectorStore();
        var embedding = new StubEmbeddingPort();
        var sut = new RagDataSeeder(embedding, store, NullLogger<RagDataSeeder>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.True(store.UpsertCount >= 200,
            $"Expected at least 200 embeddings, got {store.UpsertCount}");
    }

    [Fact]
    public async Task StartAsync_IsIdempotent_DoesNotSeedTwice()
    {
        var store = new CountingVectorStore();
        var embedding = new StubEmbeddingPort();
        var sut = new RagDataSeeder(embedding, store, NullLogger<RagDataSeeder>.Instance);

        await sut.StartAsync(CancellationToken.None);
        var firstCount = store.UpsertCount;

        await sut.StartAsync(CancellationToken.None);
        Assert.Equal(firstCount, store.UpsertCount);
    }

    [Fact]
    public async Task StartAsync_UsesMultipleTenants()
    {
        var store = new CountingVectorStore();
        var embedding = new StubEmbeddingPort();
        var sut = new RagDataSeeder(embedding, store, NullLogger<RagDataSeeder>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.True(store.TenantIds.Count >= 2,
            $"Expected at least 2 tenants, got {store.TenantIds.Count}");
    }

    // --- Test doubles ---

    private sealed class StubEmbeddingPort : IEmbeddingPort
    {
        public Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(Result<float[]>.Success(new float[384]));
    }

    private sealed class CountingVectorStore : IVectorStorePort
    {
        private bool _hasData;
        public int UpsertCount { get; private set; }
        public HashSet<Guid> TenantIds { get; } = [];

        public Task<Result<Unit>> UpsertAsync(
            Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
        {
            UpsertCount++;
            TenantIds.Add(tenantId);
            _hasData = true;
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<float[]>> GetEmbeddingAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(Result<float[]>.Success(new float[384]));

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
        {
            // Return results after first seed to make idempotency check work
            if (_hasData)
            {
                return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(
                    new List<SearchHit> { new(Guid.NewGuid(), 0.5, new Dictionary<string, string>()) }));
            }
            return Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(
                Array.Empty<SearchHit>() as IReadOnlyList<SearchHit>));
        }
    }
}
