using Api.Application.Commands;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using MassTransit;
using Messaging.Contracts.Events;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// Consumes DocumentProcessedEvent from the OCR worker and updates the
/// corresponding IntakeDocument status to PendingReview with extracted fields stored.
/// Uses FindByIdUnfilteredAsync because background consumers have no ambient
/// tenant context; manual tenant verification is performed from the event payload.
/// </summary>
public sealed class DocumentProcessedConsumer : IConsumer<DocumentProcessedEvent>
{
    private readonly IDocumentRepository _repository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly AssignDocumentToCaseHandler _assignToCaseHandler;
    private readonly ILogger<DocumentProcessedConsumer> _logger;

    public DocumentProcessedConsumer(
        IDocumentRepository repository,
        IAuditLogRepository auditLogRepository,
        AssignDocumentToCaseHandler assignToCaseHandler,
        ILogger<DocumentProcessedConsumer> logger)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _assignToCaseHandler = assignToCaseHandler;
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

        // Map extracted fields from the event message (dictionary keyed by field name).
        var extractedFields = message.ExtractedFields
            .Select(kvp => new ExtractedField(kvp.Key, kvp.Value.Value, kvp.Value.Confidence))
            .ToList();

        // Transition: Submitted -> Processing -> Completed.
        // MarkProcessing may fail if already Processing (idempotent retry); that is acceptable.
        var processingResult = document.MarkProcessing();
        if (processingResult.IsFailure && document.Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Could not mark document {message.DocumentId} as Processing " +
                $"(Status={document.Status}): {processingResult.Error.Message}");
        }

        var completedResult = document.MarkCompleted(extractedFields);
        if (completedResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Could not mark document {message.DocumentId} as Completed: " +
                completedResult.Error.Message);
        }

        // Transition to PendingReview so reviewers can act on the document.
        var pendingReviewResult = document.MarkPendingReview();
        if (pendingReviewResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Could not mark document {message.DocumentId} as PendingReview: " +
                pendingReviewResult.Error.Message);
        }

        // Attempt to assign the document to a case based on extracted name fields.
        // This mutates the in-memory entity before the single save below.
        var tenantId = new TenantId(message.TenantId);
        var documentId = new DocumentId(message.DocumentId);
        var assignResult = await _assignToCaseHandler.HandleAsync(
            documentId, tenantId, context.CancellationToken);
        if (assignResult.IsFailure)
        {
            _logger.LogWarning(
                "Case assignment failed for document {DocumentId}: {Error}. Document is still PendingReview.",
                message.DocumentId, assignResult.Error.Message);
        }

        var saveResult = await _repository.UpdateAsync(document, context.CancellationToken);
        if (saveResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to save document {message.DocumentId}: {saveResult.Error.Message}");
        }

        // Record audit entry for extraction completion.
        var auditEntry = AuditLogEntry.RecordExtractionCompleted(tenantId, documentId);
        var auditResult = await _auditLogRepository.SaveAsync(auditEntry, context.CancellationToken);
        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Audit log entry for extraction of document {DocumentId} could not be saved: {Error}",
                message.DocumentId, auditResult.Error.Message);
        }

        _logger.LogInformation(
            "Document marked as PendingReview with {FieldCount} extracted fields. DocumentId={DocumentId}",
            extractedFields.Count, message.DocumentId);
    }
}
