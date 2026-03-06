using Microsoft.Extensions.Logging.Abstractions;
using RagService.Application;
using RagService.Domain.Ports;
using RagService.Infrastructure.Messaging;
using Messaging.Contracts.Events;
using SharedKernel;

namespace RagService.Tests;

/// <summary>
/// Tests for EmbeddingRequestedConsumer verifying handler interaction paths.
/// Since ConsumeContext is a large MassTransit interface, these tests verify
/// the handler-level behavior that the consumer depends on. Full consumer
/// integration tests live in Messaging.Tests.
/// </summary>
public sealed class EmbeddingRequestedConsumerTests
{
    [Fact]
    public async Task HandleAsync_OnSuccess_ReturnsSuccessResult()
    {
        var embedding = new StubEmbeddingPort(new float[384]);
        var store = new StubVectorStore();
        var handler = new EmbedDocumentHandler(embedding, store);

        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            documentId, tenantId,
            "Patient presents with symptoms.",
            new Dictionary<string, string> { ["Diagnosis"] = "Hypertension" });

        Assert.True(result.IsSuccess);
        Assert.Single(store.Upserted);
        Assert.Equal(documentId, store.Upserted[0].DocId);
        Assert.Equal(tenantId, store.Upserted[0].TenantId);
    }

    [Fact]
    public async Task HandleAsync_OnEmbeddingFailure_ReturnsFailureResult()
    {
        var embedding = new StubEmbeddingPort(null);
        var store = new StubVectorStore();
        var handler = new EmbedDocumentHandler(embedding, store);

        var result = await handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "Some text content.",
            new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("EMBED_FAIL", result.Error.Code);
        Assert.Empty(store.Upserted);
    }

    [Fact]
    public async Task HandleAsync_OnStoreFailure_ReturnsFailureResult()
    {
        var embedding = new StubEmbeddingPort(new float[384]);
        var store = new StubVectorStore(failOnUpsert: true);
        var handler = new EmbedDocumentHandler(embedding, store);

        var result = await handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "Document text.",
            new Dictionary<string, string> { ["Field"] = "Value" });

        Assert.True(result.IsFailure);
        Assert.Equal("STORE_FAIL", result.Error.Code);
    }

    [Fact]
    public void Consumer_CanBeConstructed_WithHandlerAndLogger()
    {
        var embedding = new StubEmbeddingPort(new float[384]);
        var store = new StubVectorStore();
        var handler = new EmbedDocumentHandler(embedding, store);
        var logger = NullLogger<EmbeddingRequestedConsumer>.Instance;

        var consumer = new EmbeddingRequestedConsumer(handler, logger);

        Assert.NotNull(consumer);
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

        public Task<Result<float[]>> GetEmbeddingAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult(Result<float[]>.Success(new float[384]));

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(
                Array.Empty<SearchHit>()));
    }
}
