using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OcrWorker.Infrastructure.Messaging;

namespace Messaging.Tests.Consumers;

/// <summary>
/// Verifies that DocumentUploadedConsumer receives a published event and emits
/// a DocumentProcessedEvent in response. Uses MassTransit's InMemory test harness
/// to avoid a real RabbitMQ connection.
/// </summary>
public sealed class DocumentUploadedConsumerTests
{
    [Fact]
    public async Task DocumentUploadedConsumer_WhenMessagePublished_ConsumesAndPublishesProcessedEvent()
    {
        await using var provider = new ServiceCollection()
            .AddLogging(b => b.AddProvider(NullLoggerProvider.Instance))
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentUploadedConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var documentId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            await harness.Bus.Publish(new DocumentUploadedEvent(
                DocumentId: documentId,
                TemplateId: Guid.NewGuid(),
                TenantId: tenantId,
                FileName: "test-intake.pdf",
                UploadedAt: DateTimeOffset.UtcNow));

            // Consumer received the event
            Assert.True(await harness.Consumed.Any<DocumentUploadedEvent>());

            // Consumer published a downstream DocumentProcessedEvent
            Assert.True(await harness.Published.Any<DocumentProcessedEvent>());

            var published = harness.Published
                .Select<DocumentProcessedEvent>()
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
    public async Task DocumentUploadedConsumer_NoFaults_WhenMessageIsValid()
    {
        await using var provider = new ServiceCollection()
            .AddLogging(b => b.AddProvider(NullLoggerProvider.Instance))
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentUploadedConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new DocumentUploadedEvent(
                DocumentId: Guid.NewGuid(),
                TemplateId: Guid.NewGuid(),
                TenantId: Guid.NewGuid(),
                FileName: "valid.pdf",
                UploadedAt: DateTimeOffset.UtcNow));

            Assert.True(await harness.Consumed.Any<DocumentUploadedEvent>());
            Assert.False(await harness.Published.Any<Fault<DocumentUploadedEvent>>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
