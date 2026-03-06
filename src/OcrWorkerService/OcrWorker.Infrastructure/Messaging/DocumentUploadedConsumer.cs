using System.Diagnostics;
using MassTransit;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using Microsoft.Extensions.Logging;
using OcrWorker.Application;
using SharedKernel.Diagnostics;

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
    private readonly AppMetrics? _metrics;

    public DocumentUploadedConsumer(
        ILogger<DocumentUploadedConsumer> logger,
        ProcessDocumentHandler handler,
        AppMetrics? metrics = null)
    {
        _logger = logger;
        _handler = handler;
        _metrics = metrics;
    }

    public async Task Consume(ConsumeContext<DocumentUploadedEvent> context)
    {
        var message = context.Message;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = message.TenantId,
            ["DocumentId"] = message.DocumentId,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        }))
        {
            _logger.LogInformation(
                "Received DocumentUploadedEvent. DocumentId={DocumentId} TenantId={TenantId} FileName={FileName}",
                message.DocumentId,
                message.TenantId,
                message.FileName);

            // TODO: Fetch actual document bytes from file storage (IFileStoragePort) once
            // a real OCR adapter replaces MockOcrAdapter. The mock ignores stream content.
            using var stream = new MemoryStream();
            var sw = Stopwatch.StartNew();
            var ocrResult = await _handler.HandleAsync(stream, message.FileName, context.CancellationToken);
            sw.Stop();
            _metrics?.OcrProcessingDuration.Record(sw.Elapsed.TotalMilliseconds);

            if (ocrResult.IsFailure)
            {
                _metrics?.OcrFailureCount.Add(1);
                _logger.LogError(
                    "OCR processing failed for DocumentId={DocumentId}: {Error}",
                    message.DocumentId, ocrResult.Error.Message);
                throw new InvalidOperationException(ocrResult.Error.Message);
            }

            _metrics?.OcrSuccessCount.Add(1);

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
}
