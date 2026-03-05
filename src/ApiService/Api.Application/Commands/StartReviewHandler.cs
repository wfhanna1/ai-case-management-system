using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class StartReviewHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<StartReviewHandler> _logger;

    public StartReviewHandler(
        IDocumentRepository documentRepository,
        IAuditLogRepository auditLogRepository,
        ILogger<StartReviewHandler> logger)
    {
        _documentRepository = documentRepository;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<Result<Unit>> HandleAsync(
        Guid documentId,
        Guid tenantId,
        Guid reviewerUserId,
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

        var startResult = document.StartReview(reviewerId);
        if (startResult.IsFailure)
            return Result<Unit>.Failure(startResult.Error);

        var saveResult = await _documentRepository.UpdateAsync(document, ct);
        if (saveResult.IsFailure)
            return Result<Unit>.Failure(saveResult.Error);

        var auditEntry = AuditLogEntry.RecordReviewStarted(tid, did, reviewerId);
        var auditResult = await _auditLogRepository.SaveAsync(auditEntry, ct);
        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Audit log entry for review start of document {DocumentId} could not be saved: {Error}",
                documentId, auditResult.Error.Message);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
