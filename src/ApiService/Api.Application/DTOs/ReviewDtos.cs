namespace Api.Application.DTOs;

public sealed record ExtractedFieldDto(
    string Name,
    string Value,
    double Confidence,
    string? CorrectedValue);

public sealed record ReviewDocumentDto(
    Guid Id,
    Guid TenantId,
    string OriginalFileName,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ProcessedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    IReadOnlyList<ExtractedFieldDto> ExtractedFields);

public sealed record AuditLogEntryDto(
    Guid Id,
    string Action,
    Guid? PerformedBy,
    DateTimeOffset Timestamp,
    string? FieldName,
    string? PreviousValue,
    string? NewValue);

public sealed record RecentActivityDto(
    Guid Id,
    Guid DocumentId,
    string Action,
    Guid? PerformedBy,
    DateTimeOffset Timestamp,
    string? FieldName,
    string? PreviousValue,
    string? NewValue);

public sealed record PendingReviewResultDto(
    IReadOnlyList<ReviewDocumentDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record CorrectFieldRequest(
    string FieldName,
    string NewValue);
