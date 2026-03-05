namespace Api.Application.DTOs;

public sealed record SimilarCaseDto(
    Guid DocumentId,
    double Score,
    string Summary,
    Dictionary<string, string> Metadata,
    Dictionary<string, string> SharedFields);

public sealed record SimilarCasesResultDto(
    IReadOnlyList<SimilarCaseDto> Items);
