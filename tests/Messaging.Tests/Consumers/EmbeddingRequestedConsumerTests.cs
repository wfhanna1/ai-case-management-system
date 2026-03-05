using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagService.Application;
using RagService.Domain.Ports;
using RagService.Infrastructure.Embeddings;
using RagService.Infrastructure.Messaging;
using SharedKernel;

namespace Messaging.Tests.Consumers;

/// <summary>
/// Verifies that EmbeddingRequestedConsumer receives a published event and emits
/// an EmbeddingCompletedEvent in response. Uses MassTransit's InMemory test harness.
/// </summary>
public sealed class EmbeddingRequestedConsumerTests
{
    [Fact]
    public async Task EmbeddingRequestedConsumer_WhenMessagePublished_ConsumesAndPublishesCompletedEvent()
    {
        await using var provider = BuildProvider();

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var documentId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            await harness.Bus.Publish(new EmbeddingRequestedEvent(
                DocumentId: documentId,
                TenantId: tenantId,
                TextContent: "Patient presents with hypertension.",
                FieldValues: new Dictionary<string, string> { ["Condition"] = "Hypertension" },
                RequestedAt: DateTimeOffset.UtcNow));

            // Consumer received the event
            Assert.True(await harness.Consumed.Any<EmbeddingRequestedEvent>());

            // Consumer published a downstream EmbeddingCompletedEvent
            Assert.True(await harness.Published.Any<EmbeddingCompletedEvent>());

            var published = harness.Published
                .Select<EmbeddingCompletedEvent>()
                .First();

            Assert.Equal(documentId, published.Context.Message.DocumentId);
            Assert.Equal(tenantId, published.Context.Message.TenantId);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task EmbeddingRequestedConsumer_NoFaults_WhenMessageIsValid()
    {
        await using var provider = BuildProvider();

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new EmbeddingRequestedEvent(
                DocumentId: Guid.NewGuid(),
                TenantId: Guid.NewGuid(),
                TextContent: "Some extracted text.",
                FieldValues: new Dictionary<string, string>(),
                RequestedAt: DateTimeOffset.UtcNow));

            Assert.True(await harness.Consumed.Any<EmbeddingRequestedEvent>());
            Assert.False(await harness.Published.Any<Fault<EmbeddingRequestedEvent>>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static ServiceProvider BuildProvider()
    {
        return new ServiceCollection()
            .AddLogging(b => b.AddProvider(NullLoggerProvider.Instance))
            .AddSingleton<IEmbeddingPort, MockEmbeddingAdapter>()
            .AddSingleton<IVectorStorePort, InMemoryVectorStore>()
            .AddSingleton<EmbedDocumentHandler>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<EmbeddingRequestedConsumer>();
            })
            .BuildServiceProvider(true);
    }

    /// <summary>In-memory vector store for testing (no Qdrant dependency).</summary>
    private sealed class InMemoryVectorStore : IVectorStorePort
    {
        public Task<Result<Unit>> UpsertAsync(
            Guid documentId, Guid tenantId, float[] embedding,
            Dictionary<string, string> metadata, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(
            Guid tenantId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<SearchHit>>.Success(
                Array.Empty<SearchHit>()));
    }
}
