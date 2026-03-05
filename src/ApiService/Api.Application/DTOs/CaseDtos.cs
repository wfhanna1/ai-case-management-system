namespace Api.Application.DTOs;

public sealed record CaseDto(
    Guid Id,
    Guid TenantId,
    string SubjectName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int DocumentCount);

public sealed record CaseDetailDto(
    Guid Id,
    Guid TenantId,
    string SubjectName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DocumentDto> Documents);

public sealed record SearchCasesResultDto(
    IReadOnlyList<CaseDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
