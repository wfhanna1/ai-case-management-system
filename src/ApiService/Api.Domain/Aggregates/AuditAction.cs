namespace Api.Domain.Aggregates;

/// <summary>
/// The type of action recorded in the audit log.
/// </summary>
public enum AuditAction
{
    ExtractionCompleted,
    ReviewStarted,
    FieldCorrected,
    ReviewFinalized
}
