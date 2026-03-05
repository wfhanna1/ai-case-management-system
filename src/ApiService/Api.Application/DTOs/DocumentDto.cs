namespace Api.Application.DTOs;

public sealed record DocumentDto(
    Guid Id,
    Guid TenantId,
    string OriginalFileName,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ProcessedAt);
