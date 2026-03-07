using System.Diagnostics;
using MassTransit;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using Microsoft.Extensions.Logging;
using OcrWorker.Application;
using OcrWorker.Domain.Ports;
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
    private readonly IFileStorageReadPort _fileStorage;
    private readonly AppMetrics? _metrics;

    public DocumentUploadedConsumer(
        ILogger<DocumentUploadedConsumer> logger,
        ProcessDocumentHandler handler,
        IFileStorageReadPort fileStorage,
        AppMetrics? metrics = null)
    {
        _logger = logger;
        _handler = handler;
        _fileStorage = fileStorage;
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

            var downloadResult = await _fileStorage.DownloadAsync(message.StorageKey, context.CancellationToken);
            if (downloadResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to download file for DocumentId={DocumentId}: {Error}",
                    message.DocumentId, downloadResult.Error.Message);
                throw new InvalidOperationException(downloadResult.Error.Message);
            }

            using var stream = downloadResult.Value;
            var sw = Stopwatch.StartNew();
            var ocrResult = await _handler.HandleAsync(stream, message.FileName, context.CancellationToken);
            sw.Stop();
            _metrics?.OcrProcessingDuration.Record(sw.Elapsed.TotalMilliseconds);

            Dictionary<string, ExtractedFieldResult> extractedFields;

            if (ocrResult.IsFailure)
            {
                _metrics?.OcrFailureCount.Add(1);
                _logger.LogWarning(
                    "OCR processing failed for DocumentId={DocumentId}: {Error}. Publishing with empty fields.",
                    message.DocumentId, ocrResult.Error.Message);
                extractedFields = new Dictionary<string, ExtractedFieldResult>();
            }
            else
            {
                _metrics?.OcrSuccessCount.Add(1);
                _logger.LogInformation(
                    "OCR raw text for DocumentId={DocumentId} ({Length} chars): {RawText}",
                    message.DocumentId,
                    ocrResult.Value.RawText.Length,
                    ocrResult.Value.RawText.Length > 2000
                        ? ocrResult.Value.RawText[..2000] + "..."
                        : ocrResult.Value.RawText);
                extractedFields = ocrResult.Value.Fields.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ExtractedFieldResult(kvp.Value.FieldName, kvp.Value.Value, kvp.Value.Confidence));
            }

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
