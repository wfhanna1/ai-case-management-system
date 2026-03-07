using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class CompleteDocumentProcessingHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly AssignDocumentToCaseHandler _assignToCaseHandler;
    private readonly ILogger<CompleteDocumentProcessingHandler> _logger;

    public CompleteDocumentProcessingHandler(
        IDocumentRepository documentRepository,
        IAuditLogRepository auditLogRepository,
        AssignDocumentToCaseHandler assignToCaseHandler,
        ILogger<CompleteDocumentProcessingHandler> logger)
    {
        _documentRepository = documentRepository;
        _auditLogRepository = auditLogRepository;
        _assignToCaseHandler = assignToCaseHandler;
        _logger = logger;
    }

    public async Task<Result<Unit>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        IReadOnlyDictionary<string, (string Value, double Confidence)> extractedFields,
        CancellationToken ct = default)
    {
        var did = new DocumentId(documentId);
        var tid = new TenantId(tenantId);

        var findResult = await _documentRepository.FindByIdUnfilteredAsync(did, ct);
        if (findResult.IsFailure)
            return Result<Unit>.Failure(findResult.Error);

        var document = findResult.Value;
        if (document is null)
            return Result<Unit>.Failure(new Error("NOT_FOUND", $"Document {documentId} not found."));

        if (document.TenantId != tid)
            return Result<Unit>.Failure(new Error("TENANT_MISMATCH",
                "Document does not belong to the specified tenant."));

        var fields = extractedFields
            .Select(kvp => new ExtractedField(kvp.Key, kvp.Value.Value, kvp.Value.Confidence))
            .ToList();

        // Transition: Submitted -> Processing -> Completed -> PendingReview.
        // MarkProcessing may fail if already Processing (idempotent retry); that is acceptable.
        var processingResult = document.MarkProcessing();
        if (processingResult.IsFailure && document.Status != DocumentStatus.Processing)
        {
            return Result<Unit>.Failure(new Error("INVALID_TRANSITION",
                $"Could not mark document as Processing (Status={document.Status})."));
        }

        var completedResult = document.MarkCompleted(fields);
        if (completedResult.IsFailure)
            return Result<Unit>.Failure(completedResult.Error);

        var pendingReviewResult = document.MarkPendingReview();
        if (pendingReviewResult.IsFailure)
            return Result<Unit>.Failure(pendingReviewResult.Error);

        // Attempt case assignment based on extracted name fields.
        var assignResult = await _assignToCaseHandler.HandleAsync(did, tid, ct);
        if (assignResult.IsFailure)
        {
            _logger.LogWarning(
                "Case assignment failed for document {DocumentId}: {Error}. Document is still PendingReview.",
                documentId, assignResult.Error.Message);
        }

        var saveResult = await _documentRepository.UpdateAsync(document, ct);
        if (saveResult.IsFailure)
            return Result<Unit>.Failure(saveResult.Error);

        var auditEntry = AuditLogEntry.RecordExtractionCompleted(tid, did);
        var auditResult = await _auditLogRepository.SaveAsync(auditEntry, ct);
        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Audit log entry for extraction of document {DocumentId} could not be saved: {Error}",
                documentId, auditResult.Error.Message);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
