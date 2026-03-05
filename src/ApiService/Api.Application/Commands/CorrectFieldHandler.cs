using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class CorrectFieldHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<CorrectFieldHandler> _logger;

    public CorrectFieldHandler(
        IDocumentRepository documentRepository,
        IAuditLogRepository auditLogRepository,
        ILogger<CorrectFieldHandler> logger)
    {
        _documentRepository = documentRepository;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<Result<Unit>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        Guid reviewerUserId,
        string fieldName,
        string newValue,
        CancellationToken ct = default)
    {
        var tid = new TenantId(tenantId);
        var did = new DocumentId(documentId);
        var reviewerId = new UserId(reviewerUserId);

        var findResult = await _documentRepository.FindByIdAsync(did, tid, ct);
        if (findResult.IsFailure)
            return Result<Unit>.Failure(findResult.Error);

        if (findResult.Value is null)
            return Result<Unit>.Failure(new Error("NOT_FOUND", "Document not found."));

        var document = findResult.Value;

        var correctResult = document.CorrectField(fieldName, newValue, reviewerId);
        if (correctResult.IsFailure)
            return Result<Unit>.Failure(correctResult.Error);

        var (previousValue, correctedValue) = correctResult.Value;

        var saveDocResult = await _documentRepository.UpdateAsync(document, ct);
        if (saveDocResult.IsFailure)
            return Result<Unit>.Failure(saveDocResult.Error);

        var auditEntry = AuditLogEntry.RecordFieldCorrected(
            tid, did, reviewerId, fieldName, previousValue, correctedValue);

        var auditResult = await _auditLogRepository.SaveAsync(auditEntry, ct);
        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Audit log entry for field correction on document {DocumentId} could not be saved: {Error}",
                documentId, auditResult.Error.Message);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
