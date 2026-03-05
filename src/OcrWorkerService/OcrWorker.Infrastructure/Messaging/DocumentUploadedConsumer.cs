using MassTransit;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using Microsoft.Extensions.Logging;
using OcrWorker.Application;

namespace OcrWorker.Infrastructure.Messaging;

/// <summary>
/// Consumes DocumentUploadedEvent messages from the message bus and triggers OCR processing.
/// After processing it publishes a DocumentProcessedEvent via the ConsumeContext
/// so the outbound message participates in the same transport lifecycle (outbox, retries).
/// </summary>
public sealed class DocumentUploadedConsumer : IConsumer<DocumentUploadedEvent>
{
    private readonly ILogger<DocumentUploadedConsumer> _logger;
    private readonly ProcessDocumentHandler _handler;

    public DocumentUploadedConsumer(
        ILogger<DocumentUploadedConsumer> logger,
        ProcessDocumentHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    public async Task Consume(ConsumeContext<DocumentUploadedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received DocumentUploadedEvent. DocumentId={DocumentId} TenantId={TenantId} FileName={FileName}",
            message.DocumentId,
            message.TenantId,
            message.FileName);

        // TODO: Fetch actual document bytes from file storage (IFileStoragePort) once
        // a real OCR adapter replaces MockOcrAdapter. The mock ignores stream content.
        using var stream = new MemoryStream();
        var ocrResult = await _handler.HandleAsync(stream, message.FileName, context.CancellationToken);

        if (ocrResult.IsFailure)
        {
            _logger.LogError(
                "OCR processing failed for DocumentId={DocumentId}: {Error}",
                message.DocumentId, ocrResult.Error.Message);
            throw new InvalidOperationException(ocrResult.Error.Message);
        }

        var extractedFields = ocrResult.Value.Fields.ToDictionary(
            kvp => kvp.Key,
            kvp => new ExtractedFieldResult(kvp.Value.FieldName, kvp.Value.Value, kvp.Value.Confidence));

        var processed = new DocumentProcessedEvent(
            DocumentId: message.DocumentId,
            TenantId: message.TenantId,
            ExtractedFields: extractedFields,
            ProcessedAt: DateTimeOffset.UtcNow);

        await context.Publish(processed, context.CancellationToken);

        _logger.LogInformation(
            "Published DocumentProcessedEvent. DocumentId={DocumentId} FieldCount={FieldCount}",
            message.DocumentId,
            extractedFields.Count);
    }
}
