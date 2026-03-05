using Api.Application.DTOs;
using Api.Domain.Aggregates;

namespace Api.Application.Mappings;

public static class ReviewMappings
{
    public static ReviewDocumentDto ToDto(IntakeDocument document) =>
        new(document.Id.Value,
            document.TenantId.Value,
            document.OriginalFileName,
            document.Status.ToString(),
            document.SubmittedAt,
            document.ProcessedAt,
            document.ReviewedBy?.Value,
            document.ReviewedAt,
            document.ExtractedFields
                .Select(f => new ExtractedFieldDto(f.Name, f.Value, f.Confidence, f.CorrectedValue))
                .ToList());

    public static AuditLogEntryDto ToDto(AuditLogEntry entry) =>
        new(entry.Id,
            entry.Action.ToString(),
            entry.PerformedBy?.Value,
            entry.Timestamp,
            entry.FieldName,
            entry.PreviousValue,
            entry.NewValue);
}
