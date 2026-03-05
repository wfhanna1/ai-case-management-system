using MassTransit;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace OcrWorker.Infrastructure.Messaging;

/// <summary>
/// Consumes DocumentUploadedEvent messages from the message bus and triggers OCR processing.
/// After (stub) processing it publishes a DocumentProcessedEvent via the ConsumeContext
/// so the outbound message participates in the same transport lifecycle (outbox, retries).
/// </summary>
public sealed class DocumentUploadedConsumer : IConsumer<DocumentUploadedEvent>
{
    private readonly ILogger<DocumentUploadedConsumer> _logger;

    public DocumentUploadedConsumer(ILogger<DocumentUploadedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentUploadedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received DocumentUploadedEvent. DocumentId={DocumentId} TenantId={TenantId} FileName={FileName}",
            message.DocumentId,
            message.TenantId,
            message.FileName);

        // TODO: inject and invoke actual OCR use case from OcrWorker.Application.
        // Stub: emit an empty extraction result to keep the pipeline flowing.
        var extractedFields = new Dictionary<string, ExtractedFieldResult>();

        var processed = new DocumentProcessedEvent(
            DocumentId: message.DocumentId,
            TenantId: message.TenantId,
            ExtractedFields: extractedFields,
            ProcessedAt: DateTimeOffset.UtcNow);

        await context.Publish(processed, context.CancellationToken);

        _logger.LogInformation(
            "Published DocumentProcessedEvent. DocumentId={DocumentId}",
            message.DocumentId);
    }
}
