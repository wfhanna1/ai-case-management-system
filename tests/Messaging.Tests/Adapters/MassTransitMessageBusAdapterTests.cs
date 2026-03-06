using Api.Domain.Aggregates;
using Api.Infrastructure.Messaging;
using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Messaging.Tests.Adapters;

/// <summary>
/// Verifies that MassTransitMessageBusAdapter correctly translates domain publish calls
/// into MassTransit Publish calls using the InMemory test harness.
/// </summary>
public sealed class MassTransitMessageBusAdapterTests
{
    [Fact]
    public async Task PublishDocumentUploadedAsync_PublishesDocumentUploadedEvent()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var adapter = new MassTransitMessageBusAdapter(harness.Bus);

            var documentId = DocumentId.New();
            var templateId = Guid.NewGuid();
            var tenantId = TenantId.New();

            var result = await adapter.PublishDocumentUploadedAsync(
                documentId: documentId,
                templateId: templateId,
                tenantId: tenantId,
                fileName: "patient-form.pdf",
                storageKey: "tenants/abc/patient-form.pdf");

            Assert.True(result.IsSuccess);
            Assert.True(await harness.Published.Any<DocumentUploadedEvent>());

            var published = harness.Published
                .Select<DocumentUploadedEvent>()
                .First();

            Assert.Equal(documentId.Value, published.Context.Message.DocumentId);
            Assert.Equal(templateId, published.Context.Message.TemplateId);
            Assert.Equal(tenantId.Value, published.Context.Message.TenantId);
            Assert.Equal("patient-form.pdf", published.Context.Message.FileName);
            Assert.Equal("tenants/abc/patient-form.pdf", published.Context.Message.StorageKey);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PublishEmbeddingRequestedAsync_PublishesEmbeddingRequestedEvent()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var adapter = new MassTransitMessageBusAdapter(harness.Bus);

            var documentId = DocumentId.New();
            var tenantId = TenantId.New();
            var fieldValues = new Dictionary<string, string>
            {
                ["PatientName"] = "Jane Doe",
                ["Condition"] = "Hypertension"
            };

            var result = await adapter.PublishEmbeddingRequestedAsync(
                documentId: documentId,
                tenantId: tenantId,
                textContent: "Patient Jane Doe with hypertension.",
                fieldValues: fieldValues);

            Assert.True(result.IsSuccess);
            Assert.True(await harness.Published.Any<EmbeddingRequestedEvent>());

            var published = harness.Published
                .Select<EmbeddingRequestedEvent>()
                .First();

            Assert.Equal(documentId.Value, published.Context.Message.DocumentId);
            Assert.Equal(tenantId.Value, published.Context.Message.TenantId);
            Assert.Equal("Patient Jane Doe with hypertension.", published.Context.Message.TextContent);
            Assert.Equal(2, published.Context.Message.FieldValues.Count);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PublishDocumentUploadedAsync_ReturnsFailure_WhenPublishThrows()
    {
        // AlwaysThrowingPublishEndpoint simulates a bus that cannot connect.
        var brokenEndpoint = new AlwaysThrowingPublishEndpoint();
        var adapter = new MassTransitMessageBusAdapter(brokenEndpoint);

        var result = await adapter.PublishDocumentUploadedAsync(
            documentId: DocumentId.New(),
            templateId: Guid.NewGuid(),
            tenantId: TenantId.New(),
            fileName: "file.pdf",
            storageKey: "tenants/abc/file.pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("PUBLISH_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task PublishEmbeddingRequestedAsync_ReturnsFailure_WhenPublishThrows()
    {
        var brokenEndpoint = new AlwaysThrowingPublishEndpoint();
        var adapter = new MassTransitMessageBusAdapter(brokenEndpoint);

        var result = await adapter.PublishEmbeddingRequestedAsync(
            documentId: DocumentId.New(),
            tenantId: TenantId.New(),
            textContent: "text",
            fieldValues: new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal("PUBLISH_FAILED", result.Error.Code);
    }

    /// <summary>
    /// Test double that simulates a faulted message bus by throwing on all publish paths.
    /// Implements the full IPublishEndpoint interface so the compiler is satisfied.
    /// </summary>
    private sealed class AlwaysThrowingPublishEndpoint : IPublishEndpoint
    {
        private static Exception Boom() => new InvalidOperationException("Simulated bus failure.");

        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => throw Boom();

        // Strongly-typed overloads used by MassTransit internally and by extension methods.
        public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class => throw Boom();
        public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class => throw Boom();
        public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class => throw Boom();

        // Object-typed overloads required by the interface.
        public Task Publish(object message, CancellationToken cancellationToken = default) => throw Boom();
        public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) => throw Boom();
        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) => throw Boom();
        public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) => throw Boom();

        // Generic object-typed overloads the compiler is complaining about.
        public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class => throw Boom();
        public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class => throw Boom();
        public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class => throw Boom();
    }
}
