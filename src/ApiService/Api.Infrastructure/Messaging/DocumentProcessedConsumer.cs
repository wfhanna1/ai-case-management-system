using System.Diagnostics;
using Api.Application.Commands;
using Api.Domain.Aggregates;
using MassTransit;
using Messaging.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// Consumes DocumentProcessedEvent from the OCR worker and delegates
/// business orchestration to CompleteDocumentProcessingHandler.
/// This consumer is responsible only for message deserialization,
/// logging, and publishing downstream events.
/// </summary>
public sealed class DocumentProcessedConsumer : IConsumer<DocumentProcessedEvent>
{
    private readonly CompleteDocumentProcessingHandler _handler;
    private readonly ILogger<DocumentProcessedConsumer> _logger;

    public DocumentProcessedConsumer(
        CompleteDocumentProcessingHandler handler,
        ILogger<DocumentProcessedConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentProcessedEvent> context)
    {
        var message = context.Message;

        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = message.TenantId,
            ["DocumentId"] = message.DocumentId,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        });

        _logger.LogInformation(
            "Received DocumentProcessedEvent. DocumentId={DocumentId} TenantId={TenantId} FieldCount={FieldCount}",
            message.DocumentId,
            message.TenantId,
            message.ExtractedFields.Count);

        var fields = message.ExtractedFields
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.Value, kvp.Value.Confidence))
            as IReadOnlyDictionary<string, (string Value, double Confidence)>;

        var result = await _handler.HandleAsync(
            message.DocumentId,
            message.TenantId,
            fields!,
            context.CancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to process document {message.DocumentId}: {result.Error.Message}");
        }

        _logger.LogInformation(
            "Document marked as PendingReview with {FieldCount} extracted fields. DocumentId={DocumentId}",
            message.ExtractedFields.Count, message.DocumentId);

        // Publish embedding request so RagService can generate vector embeddings for similarity search.
        if (message.ExtractedFields.Count > 0)
        {
            var textContent = string.Join("\n", message.ExtractedFields.Select(f => $"{f.Key}: {f.Value.Value}"));
            var fieldValues = message.ExtractedFields.ToDictionary(f => f.Key, f => f.Value.Value);

            var embeddingEvent = new EmbeddingRequestedEvent(
                DocumentId: message.DocumentId,
                TenantId: message.TenantId,
                TextContent: textContent,
                FieldValues: fieldValues,
                RequestedAt: DateTimeOffset.UtcNow);

            await context.Publish(embeddingEvent, context.CancellationToken);

            _logger.LogInformation(
                "Published EmbeddingRequestedEvent for DocumentId={DocumentId}",
                message.DocumentId);
        }
    }
}
