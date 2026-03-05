using MassTransit;
using Messaging.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace RagService.Infrastructure.Messaging;

/// <summary>
/// Consumes EmbeddingRequestedEvent messages and triggers vector embedding and storage.
/// After (stub) processing it publishes EmbeddingCompletedEvent so the pipeline can
/// mark the document as fully indexed.
/// </summary>
public sealed class EmbeddingRequestedConsumer : IConsumer<EmbeddingRequestedEvent>
{
    private readonly ILogger<EmbeddingRequestedConsumer> _logger;

    public EmbeddingRequestedConsumer(ILogger<EmbeddingRequestedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EmbeddingRequestedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received EmbeddingRequestedEvent. DocumentId={DocumentId} TenantId={TenantId} FieldCount={FieldCount}",
            message.DocumentId,
            message.TenantId,
            message.FieldValues.Count);

        // TODO: inject and invoke actual embedding use case from RagService.Application.
        // Stub: acknowledge receipt and signal completion.

        var completed = new EmbeddingCompletedEvent(
            DocumentId: message.DocumentId,
            TenantId: message.TenantId,
            CompletedAt: DateTimeOffset.UtcNow);

        await context.Publish(completed, context.CancellationToken);

        _logger.LogInformation(
            "Published EmbeddingCompletedEvent. DocumentId={DocumentId}",
            message.DocumentId);
    }
}
