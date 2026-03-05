using SharedKernel;

namespace Api.Domain.Aggregates;

/// <summary>
/// Records a single auditable event in the document review workflow.
/// Append-only: entries are never updated or deleted.
/// </summary>
public sealed class AuditLogEntry : Entity<Guid>
{
    public TenantId TenantId { get; private set; }
    public DocumentId DocumentId { get; private set; }
    public AuditAction Action { get; private set; }
    public UserId? PerformedBy { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? FieldName { get; private set; }
    public string? PreviousValue { get; private set; }
    public string? NewValue { get; private set; }

    // Required by EF Core for materialization.
    private AuditLogEntry() : base(Guid.NewGuid())
    {
        TenantId = null!;
        DocumentId = null!;
    }

    private AuditLogEntry(
        Guid id,
        TenantId tenantId,
        DocumentId documentId,
        AuditAction action,
        UserId? performedBy,
        string? fieldName,
        string? previousValue,
        string? newValue) : base(id)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        Action = action;
        PerformedBy = performedBy;
        Timestamp = DateTimeOffset.UtcNow;
        FieldName = fieldName;
        PreviousValue = previousValue;
        NewValue = newValue;
    }

    public static AuditLogEntry RecordExtractionCompleted(TenantId tenantId, DocumentId documentId)
        => new(Guid.NewGuid(), tenantId, documentId, AuditAction.ExtractionCompleted,
            performedBy: null, fieldName: null, previousValue: null, newValue: null);

    public static AuditLogEntry RecordReviewStarted(TenantId tenantId, DocumentId documentId, UserId reviewerId)
        => new(Guid.NewGuid(), tenantId, documentId, AuditAction.ReviewStarted,
            reviewerId, fieldName: null, previousValue: null, newValue: null);

    public static AuditLogEntry RecordFieldCorrected(
        TenantId tenantId,
        DocumentId documentId,
        UserId reviewerId,
        string fieldName,
        string previousValue,
        string newValue)
        => new(Guid.NewGuid(), tenantId, documentId, AuditAction.FieldCorrected,
            reviewerId, fieldName, previousValue, newValue);

    public static AuditLogEntry RecordReviewFinalized(TenantId tenantId, DocumentId documentId, UserId reviewerId)
        => new(Guid.NewGuid(), tenantId, documentId, AuditAction.ReviewFinalized,
            reviewerId, fieldName: null, previousValue: null, newValue: null);
}
