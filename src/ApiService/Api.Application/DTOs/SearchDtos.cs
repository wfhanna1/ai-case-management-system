namespace Api.Application.DTOs;

public sealed record SearchDocumentsResultDto(
    IReadOnlyList<DocumentDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
