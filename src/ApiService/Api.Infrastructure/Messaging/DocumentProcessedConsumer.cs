using Api.Domain.Aggregates;
using Api.Domain.Ports;
using MassTransit;
using Messaging.Contracts.Events;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// Consumes DocumentProcessedEvent from the OCR worker and updates the
/// corresponding IntakeDocument status to Completed.
/// Uses FindByIdUnfilteredAsync because background consumers have no ambient
/// tenant context; manual tenant verification is performed from the event payload.
/// </summary>
public sealed class DocumentProcessedConsumer : IConsumer<DocumentProcessedEvent>
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<DocumentProcessedConsumer> _logger;

    public DocumentProcessedConsumer(
        IDocumentRepository repository,
        ILogger<DocumentProcessedConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DocumentProcessedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received DocumentProcessedEvent. DocumentId={DocumentId} TenantId={TenantId} FieldCount={FieldCount}",
            message.DocumentId,
            message.TenantId,
            message.ExtractedFields.Count);

        var findResult = await _repository.FindByIdUnfilteredAsync(
            new DocumentId(message.DocumentId),
            context.CancellationToken);

        if (findResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to look up document {message.DocumentId}: {findResult.Error.Message}");
        }

        var document = findResult.Value;

        if (document is null)
        {
            _logger.LogWarning(
                "Document not found for DocumentId={DocumentId}. Skipping.",
                message.DocumentId);
            return;
        }

        if (document.TenantId != new TenantId(message.TenantId))
        {
            _logger.LogWarning(
                "Tenant mismatch for DocumentId={DocumentId}. Expected={Expected} Received={Received}. Skipping.",
                message.DocumentId,
                document.TenantId.Value,
                message.TenantId);
            return;
        }

        // Transition: Submitted -> Processing -> Completed.
        // MarkProcessing may fail if already Processing (idempotent retry); that is acceptable.
        var processingResult = document.MarkProcessing();
        if (processingResult.IsFailure && document.Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Could not mark document {message.DocumentId} as Processing " +
                $"(Status={document.Status}): {processingResult.Error.Message}");
        }

        var completedResult = document.MarkCompleted();
        if (completedResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Could not mark document {message.DocumentId} as Completed: " +
                completedResult.Error.Message);
        }

        var saveResult = await _repository.UpdateAsync(document, context.CancellationToken);
        if (saveResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to save document {message.DocumentId}: {saveResult.Error.Message}");
        }

        _logger.LogInformation(
            "Document marked as Completed. DocumentId={DocumentId}",
            message.DocumentId);
    }
}
