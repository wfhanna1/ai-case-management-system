using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OcrWorker.Application;
using OcrWorker.Domain.Ports;
using OcrWorker.Infrastructure.Messaging;
using OcrWorker.Infrastructure.Ocr;
using SharedKernel;

namespace Messaging.Tests.Consumers;

/// <summary>
/// Verifies that DocumentUploadedConsumer receives a published event and emits
/// a DocumentProcessedEvent in response. Uses MassTransit's InMemory test harness
/// to avoid a real RabbitMQ connection.
/// </summary>
public sealed class DocumentUploadedConsumerTests
{
    private static ServiceProvider BuildProvider()
    {
        return new ServiceCollection()
            .AddLogging(b => b.AddProvider(NullLoggerProvider.Instance))
            .AddSingleton<IOcrPort, MockOcrAdapter>()
            .AddSingleton<IFileStorageReadPort, StubFileStorageReadPort>()
            .AddTransient<ProcessDocumentHandler>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentUploadedConsumer>();
            })
            .BuildServiceProvider(true);
    }

    private sealed class StubFileStorageReadPort : IFileStorageReadPort
    {
        public Task<Result<Stream>> DownloadAsync(string storageKey, CancellationToken ct = default)
        {
            Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("fake document content"));
            return Task.FromResult(Result<Stream>.Success(stream));
        }
    }

    [Fact]
    public async Task DocumentUploadedConsumer_WhenMessagePublished_ConsumesAndPublishesProcessedEvent()
    {
        await using var provider = BuildProvider();

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
                StorageKey: "tenants/test/test-intake.pdf",
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
            Assert.NotEmpty(published.Context.Message.ExtractedFields);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task DocumentUploadedConsumer_NoFaults_WhenMessageIsValid()
    {
        await using var provider = BuildProvider();

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new DocumentUploadedEvent(
                DocumentId: Guid.NewGuid(),
                TemplateId: Guid.NewGuid(),
                TenantId: Guid.NewGuid(),
                FileName: "valid.pdf",
                StorageKey: "tenants/test/valid.pdf",
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
